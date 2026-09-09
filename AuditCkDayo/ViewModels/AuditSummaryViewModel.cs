using System;
using System.Collections.Generic;
using System.Linq;
using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels;

public class AuditSummaryFilterViewModel
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? EstablishmentId { get; set; }
    public int? BuyerId { get; set; }
    public int? ManagerId { get; set; }
    public decimal? BeginningBalanceOverride { get; set; }
    public decimal? HandedChangeOverride { get; set; }
    public string? TableView { get; set; }
}

public class AuditSummaryViewModel
{
    public AuditSummaryFilterViewModel Filter { get; set; } = new();

    public decimal BeginningBalance { get; set; }
    public bool IsBeginningBalanceOverridden { get; set; }

    public string ActiveTableView { get; set; } = "Custodians";
    public PcfMatrixViewModel PcfMatrix { get; set; } = new();
    public PcfMatrixViewModel BranchPcfMatrix { get; set; } = new();

    public decimal TotalPc
    {
        get
        {
            var pcfTotal = PcfMatrix?.TotalPc ?? 0m;
            var managerTotal = ManagerTreasuryFlow?.CashInColumns?.Sum(c => c.Total) ?? 0m;
            return Math.Max(pcfTotal, managerTotal);
        }
    }

    public decimal TotalExpenses
    {
        get
        {
            var buyerTotal = BuyerAudits?.Sum(b => b.TotalExpenses) ?? 0m;
            var pcfTotal = PcfMatrix?.TotalExpenses ?? 0m;
            return Math.Max(buyerTotal, pcfTotal);
        }
    }
    public decimal ActualChange => TotalPc - TotalExpenses;
    public decimal HandedChange { get; set; }
    public decimal ShortOver => HandedChange - ActualChange;

    public TreasuryAuditReportViewModel ManagerTreasuryFlow { get; set; } = new();
    public List<TreasuryAuditCashOutRowViewModel> ManagerCashOutRows { get; set; } = new();
    public List<BuyerAuditReportViewModel> BuyerAudits { get; set; } = new();
    public BranchAuditReportViewModel BranchAudit { get; set; } = new();
}
