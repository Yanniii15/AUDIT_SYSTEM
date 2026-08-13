using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels;

public class ReportsFilterViewModel
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public AuditStatus? Status { get; set; }
    public int? EstablishmentId { get; set; }
    public int? BuyerId { get; set; }
    public int? TreasuryHandlerId { get; set; }
}

public class ReportsViewModel
{
    public ReportsFilterViewModel Filter { get; set; } = new();
    public string Role { get; set; } = string.Empty;
    public string ScopeLabel { get; set; } = string.Empty;

    public decimal TotalAuditAmount { get; set; }
    public decimal ApprovedAuditAmount { get; set; }
    public decimal PendingSurrenderAmount { get; set; }
    public decimal ConfirmedSurrenderAmount { get; set; }
    public decimal CurrentCashBalance { get; set; }

    public int AuditCount { get; set; }
    public int ApprovedAuditCount { get; set; }
    public int PendingAuditCount { get; set; }
    public int RejectedAuditCount { get; set; }

    public List<ReportStatusSummary> StatusSummaries { get; set; } = new();
    public List<ReportEstablishmentSummary> EstablishmentSummaries { get; set; } = new();
    public List<TreasuryReportSummary> TreasurySummaries { get; set; } = new();
    public List<AuditItem> RecentAudits { get; set; } = new();
    public List<SalesReport> RecentSalesReports { get; set; } = new();
    public List<User> CashOnHandUsers { get; set; } = new();
    public List<SurrenderRequest> SurrenderRequests { get; set; } = new();
    public List<PettyCashLedger> LedgerEntries { get; set; } = new();
    public TreasuryAuditReportViewModel TreasuryAudit { get; set; } = new();
    public BuyerAuditReportViewModel BuyerAudit { get; set; } = new();
    public List<BuyerAuditReportViewModel> BuyerAudits { get; set; } = new();
    public BranchAuditReportViewModel BranchAudit { get; set; } = new();
}

public class ReportStatusSummary
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class ReportEstablishmentSummary
{
    public string Establishment { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class TreasuryReportSummary
{
    public string Label { get; set; } = string.Empty;
    public decimal BranchTotal { get; set; }
    public decimal CostCenterTotal { get; set; }
    public decimal CashInTotal { get; set; }
    public decimal CashOutTotal { get; set; }
    public decimal ShortOverTotal { get; set; }
}

public class TreasuryAuditReportViewModel
{
    public int? TreasuryHandlerId { get; set; }
    public string TreasuryHandlerName { get; set; } = "All Treasury";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<TreasuryAuditCashInColumnViewModel> CashInColumns { get; set; } = new();
    public List<TreasuryAuditCashInRowViewModel> CashInRows { get; set; } = new();
    public List<TreasuryAuditCashOutRowViewModel> CashOutRows { get; set; } = new();
    public decimal TotalPc => CashInColumns.Sum(column => column.Total);
    public decimal TotalExpenses => CashOutRows.Sum(row => row.Amount);
    public decimal Change => TotalPc - TotalExpenses;

    public static TreasuryAuditReportViewModel Build(IEnumerable<TreasuryCashFlow> flows, int? treasuryHandlerId, DateTime startDate, DateTime endDate)
    {
        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var selectedFlows = flows
            .Where(flow => flow.CashFlowDate.Date >= startDate.Date && flow.CashFlowDate.Date <= endDate.Date)
            .Where(flow => !treasuryHandlerId.HasValue || flow.TreasuryUserId == treasuryHandlerId.Value)
            .OrderBy(flow => flow.CashFlowDate)
            .ThenBy(flow => flow.Id)
            .ToList();

        var handlerName = selectedFlows
            .Select(flow => flow.TreasuryUser?.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Selected Treasury";

        var report = new TreasuryAuditReportViewModel
        {
            TreasuryHandlerId = treasuryHandlerId,
            TreasuryHandlerName = treasuryHandlerId.HasValue ? handlerName : "All Treasury",
            StartDate = startDate.Date,
            EndDate = endDate.Date
        };

        var cashInEntries = selectedFlows
            .SelectMany(flow => flow.Entries
                .Where(entry => entry.Direction == CashFlowDirection.In)
                .Select(entry => new { Flow = flow, Entry = entry, Label = GetCashInLabel(entry) }))
            .Where(item => !string.IsNullOrWhiteSpace(item.Label))
            .ToList();

        report.CashInColumns = cashInEntries
            .GroupBy(item => item.Label)
            .Select(group => new TreasuryAuditCashInColumnViewModel
            {
                Label = group.Key,
                Total = group.Sum(item => item.Entry.Amount)
            })
            .ToList();

        report.CashInRows = cashInEntries
            .GroupBy(item => item.Flow.CashFlowDate.Date)
            .OrderBy(group => group.Key)
            .Select(group => new TreasuryAuditCashInRowViewModel
            {
                Date = group.Key,
                Amounts = group
                    .GroupBy(item => item.Label)
                    .ToDictionary(labelGroup => labelGroup.Key, labelGroup => labelGroup.Sum(item => item.Entry.Amount))
            })
            .ToList();

        report.CashOutRows = selectedFlows
            .SelectMany(flow => flow.Entries
                .Where(entry => entry.Direction == CashFlowDirection.Out)
                .Select(entry => new TreasuryAuditCashOutRowViewModel
                {
                    Date = flow.CashFlowDate.Date,
                    Description = GetCashOutDescription(entry),
                    Category = entry.Category.ToString(),
                    TreasuryHandlerName = flow.TreasuryUser?.Name ?? "Unassigned Treasury",
                    Amount = entry.Amount
                }))
            .OrderBy(row => row.Date)
            .ThenBy(row => row.Description)
            .ToList();

        return report;
    }

    private static string GetCashInLabel(CashFlowEntry entry)
    {
        if (entry.Category == CashFlowCategory.Sales && !string.IsNullOrWhiteSpace(entry.Establishment?.Name))
        {
            var establishment = entry.Establishment.Name.Trim();
            return establishment.Equals("DAYO", StringComparison.OrdinalIgnoreCase)
                ? establishment.ToUpperInvariant()
                : $"{establishment.ToUpperInvariant()} RECEIVED";
        }

        if (entry.Category == CashFlowCategory.ChangePcf && !string.IsNullOrWhiteSpace(entry.Establishment?.Name))
        {
            var establishment = entry.Establishment.Name.Trim().ToUpperInvariant();
            return establishment switch
            {
                "MAIN" => "M.CHANGE",
                "DAYO" => "D.CHANGE",
                _ => $"{establishment} CHANGE"
            };
        }

        if (!string.IsNullOrWhiteSpace(entry.RelatedUser?.Name))
        {
            return entry.RelatedUser.Name.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(entry.CostCenter?.Name))
        {
            return entry.CostCenter.Name.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(entry.Notes))
        {
            return entry.Notes.Trim().ToUpperInvariant();
        }

        return "OTHERS";
    }

    private static string GetCashOutDescription(CashFlowEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Establishment?.Name))
        {
            return entry.Establishment.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(entry.RelatedUser?.Name))
        {
            return entry.RelatedUser.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(entry.CostCenter?.Name))
        {
            return entry.CostCenter.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(entry.Notes))
        {
            return entry.Notes.Trim();
        }

        return entry.Category.ToString().ToUpperInvariant();
    }
}

public class TreasuryAuditCashInColumnViewModel
{
    public string Label { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class TreasuryAuditCashInRowViewModel
{
    public DateTime Date { get; set; }
    public Dictionary<string, decimal> Amounts { get; set; } = new();
}

public class TreasuryAuditCashOutRowViewModel
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TreasuryHandlerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
public class BuyerAuditReportViewModel
{
    public int? BuyerId { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public List<PcfReleaseLine> Releases { get; set; } = new();
    public List<BuyerExpenseLine> Expenses { get; set; } = new();
    public decimal TotalPc => Releases.Sum(r => r.Amount);
    public decimal TotalExpenses => Expenses.Sum(e => e.Amount);
    public decimal ExpectedChange => TotalPc - TotalExpenses;
    public decimal ActualChangeReturned { get; set; }
    public decimal ShortOverAmount => ActualChangeReturned - ExpectedChange;
}

public class PcfReleaseLine
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string IssuedBy { get; set; } = string.Empty;
}

public class BuyerExpenseLine
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Allocation { get; set; } = string.Empty;
}

public class BranchAuditReportViewModel
{
    public int? BranchId { get; set; }
    public string BranchName { get; set; } = "All Branches";
    public List<BranchExpenseLine> Expenses { get; set; } = new();
    public decimal TotalExpenses => Expenses.Sum(e => e.Amount);
}

public class BranchExpenseLine
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Allocation { get; set; } = string.Empty;
}
