using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuditCkDayo.Services
{
    public class SettlementExcelExportService
    {
        private readonly AuditDbContext _context;

        public SettlementExcelExportService(AuditDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> BuildWorkbookAsync(DateTime? startDate, DateTime? endDate, int? receiverUserId)
        {
            var start = (startDate ?? DateTime.Today).Date;
            var end = (endDate ?? start).Date;
            if (end < start)
            {
                (start, end) = (end, start);
            }

            var endExclusive = end.AddDays(1);

            var pcfReleasesQuery = _context.PcfReleases
                .AsNoTracking()
                .Include(r => r.ReceiverUser)
                .Include(r => r.Establishment)
                .Where(r => r.ReleaseDate >= start && r.ReleaseDate < endExclusive && r.Status != PcfReleaseStatus.Cancelled);

            var auditsQuery = _context.AuditItems
                .AsNoTracking()
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .Include(a => a.Details)
                .Where(a => a.EntryDate >= start && a.EntryDate < endExclusive && a.Status == AuditStatus.Approved);

            var surrendersQuery = _context.SurrenderRequests
                .AsNoTracking()
                .Include(s => s.Buyer)
                .Where(s => s.ActionDate.HasValue
                    && s.ActionDate.Value >= start
                    && s.ActionDate.Value < endExclusive
                    && s.Status == SurrenderStatus.Confirmed);

            if (receiverUserId.HasValue)
            {
                pcfReleasesQuery = pcfReleasesQuery.Where(r => r.ReceiverUserId == receiverUserId.Value);
                auditsQuery = auditsQuery.Where(a => a.BuyerId == receiverUserId.Value);
                surrendersQuery = surrendersQuery.Where(s => s.BuyerId == receiverUserId.Value);
            }

            var pcfReleases = await pcfReleasesQuery.OrderBy(r => r.ReleaseDate).ThenBy(r => r.Id).ToListAsync();
            var audits = await auditsQuery.OrderBy(a => a.EntryDate).ThenBy(a => a.Id).ToListAsync();
            var surrenders = await surrendersQuery.OrderBy(s => s.ActionDate).ThenBy(s => s.Id).ToListAsync();
            var cashFlows = await _context.TreasuryCashFlows
                .AsNoTracking()
                .Include(f => f.Entries)
                    .ThenInclude(e => e.Establishment)
                .Include(f => f.Entries)
                    .ThenInclude(e => e.CostCenter)
                .Include(f => f.Entries)
                    .ThenInclude(e => e.RelatedUser)
                .Include(f => f.Entries)
                    .ThenInclude(e => e.ConfirmedByUser)
                .Where(f => f.CashFlowDate >= start && f.CashFlowDate < endExclusive)
                .OrderBy(f => f.CashFlowDate)
                .ToListAsync();

            foreach (var flow in cashFlows)
            {
                flow.RecomputeTotals();
            }

            var sheets = new List<Worksheet>
            {
                BuildSettlementSheet(start, end, pcfReleases, audits, surrenders),
                BuildCashFlowSheet(start, end, cashFlows),
                BuildSourceDetailsSheet(pcfReleases, audits, surrenders, cashFlows)
            };

            return CreateWorkbook(sheets);
        }

        private static Worksheet BuildSettlementSheet(DateTime start, DateTime end, List<PcfRelease> releases, List<AuditItem> audits, List<SurrenderRequest> surrenders)
        {
            var totalPc = releases.Sum(r => r.Amount);
            var totalExpenses = audits.Sum(a => a.Amount);
            var actualChange = surrenders.Sum(s => s.ConfirmedAmount ?? s.DeclaredAmount);
            var expectedChange = totalPc - totalExpenses;
            var shortOver = actualChange - expectedChange;

            var rows = new List<Row>
            {
                Row.Title("PCF AUDIT SETTLEMENT"),
                Row.Text("PERIOD", $"{start:MMM d, yyyy} - {end:MMM d, yyyy}"),
                Row.Blank(),
                Row.Header("DESCRIPTION", "AMOUNT"),
                Row.Number("TOTAL PC", totalPc, highlight: true),
                Row.Number("TOTAL EXPENSES", totalExpenses),
                Row.Number("CHANGE", expectedChange, highlight: true),
                Row.Number("A.CHANGE", actualChange),
                Row.Number("SHORT/OVER", shortOver, highlight: true),
                Row.Blank(),
                Row.Section("PCF RELEASES"),
                Row.Header("DATE", "RECEIVER", "BRANCH", "AMOUNT", "PURPOSE")
            };

            rows.AddRange(releases.Select(r => Row.Values(
                Cell.Text(r.ReleaseDate.ToString("yyyy-MM-dd")),
                Cell.Text(r.ReceiverUser?.Name ?? r.ReceiverName ?? "Unassigned"),
                Cell.Text(r.Establishment?.Name ?? string.Empty),
                Cell.Number(r.Amount),
                Cell.Text(r.Purpose ?? string.Empty))));

            rows.Add(Row.Blank());
            rows.Add(Row.Section("APPROVED AUDIT EXPENSES"));
            rows.Add(Row.Header("DATE", "BUYER", "BRANCH", "DESCRIPTION", "AMOUNT"));
            rows.AddRange(audits.Select(a => Row.Values(
                Cell.Text(a.EntryDate.ToString("yyyy-MM-dd")),
                Cell.Text(a.Buyer.Name),
                Cell.Text(a.Establishment.Name),
                Cell.Text(a.Description),
                Cell.Number(a.Amount))));

            rows.Add(Row.Blank());
            rows.Add(Row.Section("CONFIRMED CASH SURRENDERS"));
            rows.Add(Row.Header("DATE", "BUYER", "DECLARED", "CONFIRMED", "NOTES"));
            rows.AddRange(surrenders.Select(s => Row.Values(
                Cell.Text(s.ActionDate!.Value.ToString("yyyy-MM-dd")),
                Cell.Text(s.Buyer.Name),
                Cell.Number(s.DeclaredAmount),
                Cell.Number(s.ConfirmedAmount ?? s.DeclaredAmount),
                Cell.Text(s.ActionNotes ?? string.Empty))));

            return new Worksheet("Settlement", rows);
        }

        private static Worksheet BuildCashFlowSheet(DateTime start, DateTime end, List<TreasuryCashFlow> cashFlows)
        {
            var rows = new List<Row>();
            var mergedRanges = new List<string>();

            foreach (var flow in cashFlows)
            {
                var titleRow = rows.Count + 1;
                mergedRanges.Add($"A{titleRow}:D{titleRow}");
                rows.Add(Row.Values(Cell.Title($"CKR - DAILY CASH FLOW {CashFlowLocation(flow)}"), Cell.Text(string.Empty), Cell.Text(string.Empty), Cell.Text(string.Empty)));
                rows.Add(Row.Values(Cell.Header("DATE"), Cell.Text(flow.CashFlowDate.ToString("MMM. d", CultureInfo.InvariantCulture)), Cell.Text(string.Empty), Cell.Text(string.Empty)));
                rows.Add(Row.Values(Cell.Header("STARTING BALANCE"), Cell.Number(flow.StartingBalance, highlight: true), Cell.Text(string.Empty), Cell.Text(string.Empty)));
                rows.Add(GridBlankRow());
                rows.Add(Row.Values(Cell.Header("CASH IN"), Cell.Text(string.Empty), Cell.Header("CASH OUT"), Cell.Text(string.Empty)));

                var cashInLines = BuildCashFlowTemplateLines(flow.Entries.Where(e => e.Direction == CashFlowDirection.In));
                var cashOutLines = BuildCashFlowTemplateLines(flow.Entries.Where(e => e.Direction == CashFlowDirection.Out));
                for (var i = 0; i < 26; i++)
                {
                    var cashIn = i < cashInLines.Count ? cashInLines[i] : CashFlowTemplateLine.Empty;
                    var cashOut = i < cashOutLines.Count ? cashOutLines[i] : CashFlowTemplateLine.Empty;
                    rows.Add(Row.Values(
                        Cell.Text(cashIn.Label),
                        cashIn.Amount.HasValue ? Cell.Number(cashIn.Amount.Value) : Cell.Text(string.Empty),
                        Cell.Text(cashOut.Label),
                        cashOut.Amount.HasValue ? Cell.Number(cashOut.Amount.Value) : Cell.Text(string.Empty)));
                }

                rows.Add(Row.Values(Cell.Header("TOTAL CASH IN:"), Cell.Number(flow.TotalCashIn), Cell.Header("TOTAL CASH OUT:"), Cell.Number(flow.TotalCashOut)));
                rows.Add(GridBlankRow());
                rows.Add(Row.Values(Cell.Header("NET CASH FLOW"), Cell.Number(flow.NetCashFlow, highlight: true), Cell.Text(string.Empty), Cell.Text(string.Empty)));
                rows.Add(Row.Values(Cell.Header("CLOSING BALANCE"), Cell.Number(flow.ClosingBalance, highlight: true), Cell.Text(string.Empty), Cell.Text(string.Empty)));
            }

            if (!cashFlows.Any())
            {
                rows.Add(Row.Text("No treasury cash flow entries found for this period."));
            }

            return new Worksheet("Daily Cash Flow", rows, mergedRanges);
        }

        private static Worksheet BuildSourceDetailsSheet(List<PcfRelease> releases, List<AuditItem> audits, List<SurrenderRequest> surrenders, List<TreasuryCashFlow> cashFlows)
        {
            var rows = new List<Row>
            {
                Row.Title("SOURCE DETAILS"),
                Row.Section("AUDIT EXPENSE LINE ITEMS"),
                Row.Header("AUDIT ID", "DATE", "BUYER", "BRANCH", "ITEM", "QTY", "UNIT PRICE", "LINE TOTAL")
            };

            foreach (var audit in audits)
            {
                if (audit.Details.Any())
                {
                    rows.AddRange(audit.Details.Select(d => Row.Values(
                        Cell.Text($"AUD-{audit.Id}"),
                        Cell.Text(audit.EntryDate.ToString("yyyy-MM-dd")),
                        Cell.Text(audit.Buyer.Name),
                        Cell.Text(audit.Establishment.Name),
                        Cell.Text(d.ItemName),
                        Cell.Number(d.Quantity),
                        Cell.Number(d.Price),
                        Cell.Number(d.Total))));
                }
                else
                {
                    rows.Add(Row.Values(
                        Cell.Text($"AUD-{audit.Id}"),
                        Cell.Text(audit.EntryDate.ToString("yyyy-MM-dd")),
                        Cell.Text(audit.Buyer.Name),
                        Cell.Text(audit.Establishment.Name),
                        Cell.Text(audit.Description),
                        Cell.Number(1),
                        Cell.Number(audit.Amount),
                        Cell.Number(audit.Amount)));
                }
            }

            rows.Add(Row.Blank());
            rows.Add(Row.Section("PCF RELEASES"));
            rows.Add(Row.Header("RELEASE ID", "DATE", "RECEIVER", "BRANCH", "AMOUNT", "STATUS"));
            rows.AddRange(releases.Select(r => Row.Values(
                Cell.Text($"PCF-{r.Id}"),
                Cell.Text(r.ReleaseDate.ToString("yyyy-MM-dd")),
                Cell.Text(r.ReceiverUser?.Name ?? r.ReceiverName ?? "Unassigned"),
                Cell.Text(r.Establishment?.Name ?? string.Empty),
                Cell.Number(r.Amount),
                Cell.Text(r.Status.ToString()))));

            rows.Add(Row.Blank());
            rows.Add(Row.Section("CASH SURRENDERS"));
            rows.Add(Row.Header("REQUEST ID", "DATE", "BUYER", "DECLARED", "CONFIRMED", "STATUS"));
            rows.AddRange(surrenders.Select(s => Row.Values(
                Cell.Text($"SUR-{s.Id}"),
                Cell.Text(s.ActionDate!.Value.ToString("yyyy-MM-dd")),
                Cell.Text(s.Buyer.Name),
                Cell.Number(s.DeclaredAmount),
                Cell.Number(s.ConfirmedAmount ?? s.DeclaredAmount),
                Cell.Text(s.Status.ToString()))));

            rows.Add(Row.Blank());
            rows.Add(Row.Section("TREASURY CASH FLOW ENTRIES"));
            rows.Add(Row.Header("DATE", "DIRECTION", "CATEGORY", "BRANCH/COST CENTER/USER", "AMOUNT", "NOTES"));
            rows.AddRange(cashFlows.SelectMany(f => f.Entries.OrderBy(e => e.Id).Select(e => Row.Values(
                Cell.Text(f.CashFlowDate.ToString("yyyy-MM-dd")),
                Cell.Text(e.Direction.ToString()),
                Cell.Text(e.Category.ToString()),
                Cell.Text(e.Establishment?.Name ?? e.CostCenter?.Name ?? e.RelatedUser?.Name ?? string.Empty),
                Cell.Number(e.Amount),
                Cell.Text(e.Notes ?? string.Empty)))));

            return new Worksheet("Source Details", rows);
        }

        private static string EntryLabel(CashFlowEntry? entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            var owner = entry.Establishment?.Name ?? entry.CostCenter?.Name ?? entry.RelatedUser?.Name;
            return string.IsNullOrWhiteSpace(owner) ? entry.Category.ToString() : $"{entry.Category} - {owner}";
        }

        private static Row GridBlankRow()
        {
            return Row.Values(Cell.Text(string.Empty), Cell.Text(string.Empty), Cell.Text(string.Empty), Cell.Text(string.Empty));
        }

        private static List<CashFlowTemplateLine> BuildCashFlowTemplateLines(IEnumerable<CashFlowEntry> entries)
        {
            var lines = new List<CashFlowTemplateLine>();
            foreach (var group in entries.OrderBy(e => e.Id).GroupBy(e => e.Category))
            {
                lines.Add(new CashFlowTemplateLine(CashFlowCategoryLabel(group.Key), null));
                foreach (var entry in group)
                {
                    lines.Add(new CashFlowTemplateLine(CashFlowEntryName(entry), entry.Amount));
                }
            }

            return lines;
        }

        private static string CashFlowLocation(TreasuryCashFlow flow)
        {
            var names = flow.Entries
                .Select(e => e.Establishment?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return names.Count == 1 ? names[0]! : "TREASURY";
        }

        private static string ManagerName(TreasuryCashFlow flow)
        {
            return flow.Entries
                .Select(e => e.ConfirmedByUser)
                .FirstOrDefault(u => u != null && u.Role == UserRole.Manager)
                ?.Name ?? string.Empty;
        }

        private static string CashFlowEntryName(CashFlowEntry entry)
        {
            return entry.Category == CashFlowCategory.Others && !string.IsNullOrWhiteSpace(entry.Notes)
                ? entry.Notes
                : entry.Establishment?.Name ?? entry.CostCenter?.Name ?? entry.RelatedUser?.Name ?? entry.Notes ?? string.Empty;
        }

        private static string CashFlowCategoryLabel(CashFlowCategory category)
        {
            return category switch
            {
                CashFlowCategory.PcfRelease => "PCF",
                CashFlowCategory.ChangePcf => "CHANGE PCF",
                CashFlowCategory.CashSurrender => "CASH SURRENDER",
                _ => SplitPascalCase(category.ToString()).ToUpperInvariant()
            };
        }

        private static string SplitPascalCase(string value)
        {
            var builder = new StringBuilder(value.Length + 4);
            for (var i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && !char.IsWhiteSpace(value[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(value[i]);
            }

            return builder.ToString();
        }

        private static byte[] CreateWorkbook(List<Worksheet> sheets)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddEntry(archive, "[Content_Types].xml", BuildContentTypes(sheets.Count));
                AddEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                AddEntry(archive, "xl/workbook.xml", BuildWorkbookXml(sheets));
                AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(sheets.Count));
                AddEntry(archive, "xl/styles.xml", BuildStylesXml());

                for (var i = 0; i < sheets.Count; i++)
                {
                    AddEntry(archive, $"xl/worksheets/sheet{i + 1}.xml", BuildWorksheetXml(sheets[i]));
                }
            }

            return stream.ToArray();
        }

        private static void AddEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        private static string BuildContentTypes(int sheetCount)
        {
            var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (var i = 1; i <= sheetCount; i++)
            {
                builder.Append(CultureInfo.InvariantCulture, $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }
            builder.Append("</Types>");
            return builder.ToString();
        }

        private static string BuildWorkbookXml(List<Worksheet> sheets)
        {
            var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            for (var i = 0; i < sheets.Count; i++)
            {
                builder.Append(CultureInfo.InvariantCulture, $"<sheet name=\"{XmlEscape(sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            }
            builder.Append("</sheets></workbook>");
            return builder.ToString();
        }

        private static string BuildWorkbookRelationships(int sheetCount)
        {
            var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (var i = 1; i <= sheetCount; i++)
            {
                builder.Append(CultureInfo.InvariantCulture, $"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
            }
            builder.Append(CultureInfo.InvariantCulture, $"<Relationship Id=\"rId{sheetCount + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            builder.Append("</Relationships>");
            return builder.ToString();
        }

        private static string BuildStylesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"#,##0.00\"/></numFmts>" +
                "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFFF00\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
                "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style=\"thin\"/><right style=\"thin\"/><top style=\"thin\"/><bottom style=\"thin\"/><diagonal/></border></borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\"/></cellStyleXfs>" +
                "<cellXfs count=\"5\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\"><alignment horizontal=\"center\"/></xf><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\"/><xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyBorder=\"1\"/><xf numFmtId=\"164\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/></cellXfs>" +
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles><dxfs count=\"0\"/><tableStyles count=\"0\" defaultTableStyle=\"TableStyleMedium2\" defaultPivotStyle=\"PivotStyleLight16\"/></styleSheet>";
        }

        private static string BuildWorksheetXml(Worksheet worksheet)
        {
            var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            builder.Append("<sheetViews><sheetView showGridLines=\"0\" workbookViewId=\"0\"/></sheetViews><sheetFormatPr defaultRowHeight=\"15\"/><cols><col min=\"1\" max=\"1\" width=\"22\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"14\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"28\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"14\" customWidth=\"1\"/></cols><sheetData>");
            for (var rowIndex = 0; rowIndex < worksheet.Rows.Count; rowIndex++)
            {
                var rowNumber = rowIndex + 1;
                builder.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowNumber}\">");
                var row = worksheet.Rows[rowIndex];
                for (var columnIndex = 0; columnIndex < row.Cells.Count; columnIndex++)
                {
                    builder.Append(BuildCellXml(row.Cells[columnIndex], rowNumber, columnIndex + 1));
                }
                builder.Append("</row>");
            }

            builder.Append("</sheetData>");
            if (worksheet.MergedRanges.Any())
            {
                builder.Append(CultureInfo.InvariantCulture, $"<mergeCells count=\"{worksheet.MergedRanges.Count}\">");
                foreach (var range in worksheet.MergedRanges)
                {
                    builder.Append(CultureInfo.InvariantCulture, $"<mergeCell ref=\"{range}\"/>");
                }

                builder.Append("</mergeCells>");
            }

            builder.Append("</worksheet>");
            return builder.ToString();
        }

        private static string BuildCellXml(Cell cell, int rowNumber, int columnIndex)
        {
            var reference = $"{ColumnName(columnIndex)}{rowNumber}";
            if (cell.Kind == CellKind.Empty)
            {
                return $"<c r=\"{reference}\"/>";
            }

            if (cell.Kind == CellKind.Number)
            {
                var style = cell.Highlight ? 4 : 3;
                return string.Create(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" s=\"{style}\"><v>{cell.NumberValue}</v></c>");
            }

            var text = XmlEscape(cell.TextValue ?? string.Empty);
            var stringStyle = cell.Style switch
            {
                CellStyle.Title => 1,
                CellStyle.Header => 2,
                CellStyle.Section => 1,
                _ => 0
            };
            return string.Create(CultureInfo.InvariantCulture, $"<c r=\"{reference}\" t=\"inlineStr\" s=\"{stringStyle}\"><is><t>{text}</t></is></c>");
        }

        private static string ColumnName(int index)
        {
            var name = string.Empty;
            while (index > 0)
            {
                index--;
                name = (char)('A' + index % 26) + name;
                index /= 26;
            }
            return name;
        }

        private static string XmlEscape(string value)
        {
            return SecurityElementEscape(value);
        }

        private static string SecurityElementEscape(string value)
        {
            return XmlConvert.EncodeName(value).Contains("_x", StringComparison.Ordinal) ?
                value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;") :
                value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private sealed record Worksheet(string Name, List<Row> Rows, List<string>? MergedRanges = null)
        {
            public List<string> MergedRanges { get; } = MergedRanges ?? new List<string>();
        }

        private sealed record Row(List<Cell> Cells)
        {
            public static Row Blank() => new(new List<Cell>());
            public static Row Title(string title) => new(new List<Cell> { Cell.Title(title) });
            public static Row Section(string title) => new(new List<Cell> { Cell.Section(title) });
            public static Row Text(string label, string value = "") => new(new List<Cell> { Cell.Text(label), Cell.Text(value) });
            public static Row Number(string label, decimal value, bool highlight = false) => new(new List<Cell> { Cell.Text(label), Cell.Number(value, highlight) });
            public static Row Header(params string[] values) => new(values.Select(Cell.Header).ToList());
            public static Row Values(params Cell[] cells) => new(cells.ToList());
        }

        private sealed record CashFlowTemplateLine(string Label, decimal? Amount)
        {
            public static readonly CashFlowTemplateLine Empty = new(string.Empty, null);
        }

        private sealed record Cell(CellKind Kind, string? TextValue = null, decimal NumberValue = 0m, CellStyle Style = CellStyle.Normal, bool Highlight = false)
        {
            public static Cell Empty() => new(CellKind.Empty);
            public static Cell Text(string value) => new(CellKind.Text, value);
            public static Cell Title(string value) => new(CellKind.Text, value, Style: CellStyle.Title);
            public static Cell Section(string value) => new(CellKind.Text, value, Style: CellStyle.Section);
            public static Cell Header(string value) => new(CellKind.Text, value, Style: CellStyle.Header);
            public static Cell Number(decimal value, bool highlight = false) => new(CellKind.Number, NumberValue: value, Highlight: highlight);
        }

        private enum CellKind
        {
            Empty,
            Text,
            Number
        }

        private enum CellStyle
        {
            Normal,
            Title,
            Header,
            Section
        }
    }
}
