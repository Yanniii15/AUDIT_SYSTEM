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
    public PnlReportViewModel PnlReport { get; set; } = new();
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
            .OrderBy(flow => flow.CashFlowDate)
            .ThenBy(flow => flow.Id)
            .ToList();

        var handlerName = treasuryHandlerId.HasValue
            ? selectedFlows
                .Where(flow => flow.TreasuryUserId == treasuryHandlerId.Value)
                .Select(flow => flow.TreasuryUser?.Name)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
              ?? selectedFlows
                .SelectMany(flow => flow.Entries)
                .Where(entry => entry.ReportedByUserId == treasuryHandlerId.Value)
                .Select(entry => entry.ReportedByUser?.Name)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
              ?? "Selected Treasury"
            : selectedFlows
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
                .Where(entry => entry.Direction == CashFlowDirection.In
                    && (!treasuryHandlerId.HasValue || flow.TreasuryUserId == treasuryHandlerId.Value))
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
                .Where(entry => entry.Direction == CashFlowDirection.Out
                    && (!treasuryHandlerId.HasValue
                        || flow.TreasuryUserId == treasuryHandlerId.Value
                        || entry.ReportedByUserId == treasuryHandlerId.Value))
                .Select(entry => new TreasuryAuditCashOutRowViewModel
                {
                    Date = flow.CashFlowDate.Date,
                    Description = GetCashOutDescription(entry),
                    Category = entry.Category.ToString(),
                    TreasuryHandlerName = entry.ReportedByUser?.Name ?? flow.TreasuryUser?.Name ?? "Unassigned Treasury",
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

public class PnlReportViewModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string BranchName { get; set; } = "All Branches";
    public List<PnlCategoryTotalViewModel> Categories { get; set; } = new();
    public List<PnlBranchTotalViewModel> Branches { get; set; } = new();
    public decimal TotalSales { get; set; }
    public decimal CogsTotal => Categories.Where(c => c.Section == PnlExpenseSection.COGS).Sum(c => c.Amount);
    public decimal GrossProfit => TotalSales - CogsTotal;
    public decimal OpexTotal => Categories.Where(c => c.Section == PnlExpenseSection.OPEX).Sum(c => c.Amount);
    public decimal MonthlyFixedCostTotal => Categories.Where(c => c.Section == PnlExpenseSection.MonthlyFixedCost).Sum(c => c.Amount);
    public decimal OtherTotal => Categories.Where(c => c.Section == PnlExpenseSection.Other).Sum(c => c.Amount);
    public decimal TotalExpenses => CogsTotal + OpexTotal + MonthlyFixedCostTotal + OtherTotal;
    public decimal NetProfit => GrossProfit - OpexTotal - MonthlyFixedCostTotal - OtherTotal;
    public decimal NetProfitPercentage => TotalSales == 0m ? 0m : Math.Round(NetProfit / TotalSales * 100m, 2);

    public static PnlReportViewModel Build(IEnumerable<AuditItem> auditItems, IEnumerable<SalesReport> salesReports, DateTime startDate, DateTime endDate, int? establishmentId = null)
    {
        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var confirmedSales = salesReports
            .Where(report => report.Status == SalesReportStatus.Confirmed)
            .Where(report => report.BusinessDate.Date >= startDate.Date && report.BusinessDate.Date <= endDate.Date)
            .Where(report => !establishmentId.HasValue || report.EstablishmentId == establishmentId.Value)
            .ToList();

        var approvedAudits = auditItems
            .Where(audit => audit.Status == AuditStatus.Approved)
            .Where(audit => audit.EntryDate.Date >= startDate.Date && audit.EntryDate.Date <= endDate.Date)
            .Where(audit => !establishmentId.HasValue || audit.EstablishmentId == establishmentId.Value || audit.Details.Any(detail => detail.AssignedEstablishmentId == establishmentId.Value))
            .ToList();

        var approvedDetails = approvedAudits
            .SelectMany(audit => audit.Details.Select(detail => new
            {
                Audit = audit,
                Detail = detail,
                BranchName = detail.AssignedEstablishment?.Name ?? audit.Establishment?.Name ?? "Unassigned"
            }))
            .Where(row => !establishmentId.HasValue || DetailBelongsToEstablishment(row.Audit, row.Detail, establishmentId.Value))
            .ToList();

        var model = new PnlReportViewModel
        {
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            BranchName = establishmentId.HasValue
                ? confirmedSales.Select(report => report.Establishment?.Name).Concat(approvedAudits.Select(audit => audit.Establishment?.Name)).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Selected Branch"
                : "All Branches",
            TotalSales = confirmedSales.Sum(report => report.TotalGrossSales)
        };

        model.Categories = approvedDetails
            .Select(row => row.Detail)
            .GroupBy(detail => new { Section = ResolvePnlSection(detail), CategoryName = NormalizeCategory(ResolvePnlCategoryName(detail)) })
            .Select(group => new PnlCategoryTotalViewModel
            {
                Section = group.Key.Section,
                CategoryName = group.Key.CategoryName,
                Amount = group.Sum(detail => detail.Total),
                Items = group
                    .GroupBy(detail => string.IsNullOrWhiteSpace(detail.ItemName) ? group.Key.CategoryName : detail.ItemName.Trim())
                    .Select(itemGroup => new PnlExpenseItemViewModel
                    {
                        ItemName = itemGroup.Key,
                        Amount = itemGroup.Sum(detail => detail.Total)
                    })
                    .OrderByDescending(item => item.Amount)
                    .ThenBy(item => item.ItemName)
                    .ToList()
            })
            .OrderBy(category => category.Section)
            .ThenByDescending(category => category.Amount)
            .ToList();

        var branchNames = confirmedSales.Select(report => report.Establishment?.Name ?? "Unassigned")
            .Concat(approvedDetails.Select(row => row.BranchName))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        model.Branches = branchNames
            .Select(branchName =>
            {
                var branchSales = confirmedSales.Where(report => (report.Establishment?.Name ?? "Unassigned") == branchName).ToList();
                var branchDetails = approvedDetails.Where(row => row.BranchName == branchName).Select(row => row.Detail).ToList();
                return new PnlBranchTotalViewModel
                {
                    BranchName = branchName,
                    Sales = branchSales.Sum(report => report.TotalGrossSales),
                    Cogs = branchDetails.Where(detail => ResolvePnlSection(detail) == PnlExpenseSection.COGS).Sum(detail => detail.Total),
                    Opex = branchDetails.Where(detail => ResolvePnlSection(detail) == PnlExpenseSection.OPEX).Sum(detail => detail.Total),
                    MonthlyFixedCost = branchDetails.Where(detail => ResolvePnlSection(detail) == PnlExpenseSection.MonthlyFixedCost).Sum(detail => detail.Total),
                    Other = branchDetails.Where(detail => ResolvePnlSection(detail) == PnlExpenseSection.Other).Sum(detail => detail.Total)
                };
            })
            .ToList();

        return model;
    }

    private static bool DetailBelongsToEstablishment(AuditItem audit, AuditItemDetail detail, int establishmentId)
    {
        return detail.AssignedEstablishmentId.HasValue
            ? detail.AssignedEstablishmentId.Value == establishmentId
            : audit.EstablishmentId == establishmentId;
    }

    private static PnlExpenseSection ResolvePnlSection(AuditItemDetail detail)
    {
        return detail.PnlCategory?.Section ?? detail.PnlSection;
    }

    private static string ResolvePnlCategoryName(AuditItemDetail detail)
    {
        return detail.PnlCategory?.Name ?? detail.PnlCategoryName;
    }

    private static string NormalizeCategory(string category)
    {
        return string.IsNullOrWhiteSpace(category) ? "Other" : category.Trim();
    }
}

public class PnlCategoryTotalViewModel
{
    public PnlExpenseSection Section { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<PnlExpenseItemViewModel> Items { get; set; } = new();
    public decimal PercentageOfSales(decimal totalSales) => totalSales == 0m ? 0m : Math.Round(Amount / totalSales * 100m, 2);
}

public class PnlExpenseItemViewModel
{
    public string ItemName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class PnlBranchTotalViewModel
{
    public string BranchName { get; set; } = string.Empty;
    public decimal Sales { get; set; }
    public decimal Cogs { get; set; }
    public decimal Opex { get; set; }
    public decimal MonthlyFixedCost { get; set; }
    public decimal Other { get; set; }
    public decimal GrossProfit => Sales - Cogs;
    public decimal NetProfit => GrossProfit - Opex - MonthlyFixedCost - Other;
    public decimal NetProfitPercentage => Sales == 0m ? 0m : Math.Round(NetProfit / Sales * 100m, 2);
}
