using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels;

public class ReportsFilterViewModel
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public AuditStatus? Status { get; set; }
    public int? EstablishmentId { get; set; }
    public int? BuyerId { get; set; }
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
    public List<AuditItem> RecentAudits { get; set; } = new();
    public List<SurrenderRequest> SurrenderRequests { get; set; } = new();
    public List<PettyCashLedger> LedgerEntries { get; set; } = new();
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
