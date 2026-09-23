using System;
using System.Collections.Generic;
using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels
{
    public enum DashboardRecordType
    {
        All,
        Audits,
        DailySales
    }

    public class DashboardViewModel
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public AuditStatus? Status { get; set; }
        public DashboardRecordType RecordType { get; set; } = DashboardRecordType.All;
        public int? EstablishmentId { get; set; }
        public int? BuyerId { get; set; }
        public string ActiveTab { get; set; } = "overview";
        public int HistoryPage { get; set; } = 1;
        public int HistoryPageSize { get; set; } = 30;
        public int HistoricalTotalCount { get; set; }
        public int HistoricalStartRecord => HistoricalTotalCount == 0 ? 0 : ((HistoryPage - 1) * HistoryPageSize) + 1;
        public int HistoricalEndRecord => Math.Min(HistoryPage * HistoryPageSize, HistoricalTotalCount);
        public bool HasPreviousHistoryPage => HistoryPage > 1;
        public bool HasNextHistoryPage => HistoricalEndRecord < HistoricalTotalCount;


        public List<AuditItem> Audits { get; set; } = new List<AuditItem>();
        public List<AuditItem> TodayAudits { get; set; } = new List<AuditItem>();
        public List<SalesReport> PendingSalesReports { get; set; } = new List<SalesReport>();
        public List<SalesReport> HistoricalSalesReports { get; set; } = new List<SalesReport>();
        public decimal PendingSalesGrossTotal => PendingSalesReports.Sum(r => r.TotalGrossSales);
        public decimal PendingSalesCashToHandoverTotal => PendingSalesReports.Sum(r => r.TotalConfirmedCashToHandover);
        public List<User> CashOnHandUsers { get; set; } = new List<User>();
        public decimal TotalAmount { get; set; }
    }
}
