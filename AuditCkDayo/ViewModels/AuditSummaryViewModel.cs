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
    public string SpokenSummary { get; set; } = string.Empty;

    public string ActiveTableView { get; set; } = "Custodians";
    public PcfMatrixViewModel PcfMatrix { get; set; } = new();
    public PcfMatrixViewModel BranchPcfMatrix { get; set; } = new();

    // Buyer-specific metrics (strictly from the Buyer PCF Matrix)
    public decimal BuyerTotalPc => PcfMatrix?.TotalPc ?? 0m;
    public decimal BuyerTotalExpenses => PcfMatrix?.TotalExpenses ?? (BuyerAudits?.Sum(b => b.TotalExpenses) ?? 0m);
    public decimal BuyerActualChange => BuyerTotalPc - BuyerTotalExpenses;
    public decimal BuyerHandedChange => HandedChange;
    public decimal BuyerShortOver => BuyerHandedChange - BuyerActualChange;

    // Manager-specific metrics (strictly from Manager Treasury Inflow)
    public decimal ManagerTotalIn => ManagerTreasuryFlow?.CashInColumns?.Sum(c => c.Total) ?? 0m;
    public decimal ManagerTotalOut => ManagerTreasuryFlow?.CashOutRows?.Sum(r => r.Amount) ?? 0m;
    public decimal ManagerNetFlow => ManagerTotalIn - ManagerTotalOut;

    // Active perspective metrics
    public decimal TotalPc => ActiveTableView == "Branches" ? ManagerTotalIn : BuyerTotalPc;
    public decimal TotalExpenses => ActiveTableView == "Branches" ? ManagerTotalOut : BuyerTotalExpenses;
    public decimal ActualChange => TotalPc - TotalExpenses;
    public decimal HandedChange { get; set; }
    public decimal ShortOver => ActiveTableView == "Branches" ? ManagerNetFlow : (HandedChange - ActualChange);

    public TreasuryAuditReportViewModel ManagerTreasuryFlow { get; set; } = new();
    public List<TreasuryAuditCashOutRowViewModel> ManagerCashOutRows { get; set; } = new();
    public List<BuyerAuditReportViewModel> BuyerAudits { get; set; } = new();
    public BranchAuditReportViewModel BranchAudit { get; set; } = new();
}
