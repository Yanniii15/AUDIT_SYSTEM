using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels
{
    public class TreasuryCashFlowViewModel
    {
        public DateTime CashFlowDate { get; set; } = DateTime.Today;
        public decimal StartingBalance { get; set; }
        public decimal TotalCashIn { get; set; }
        public decimal TotalCashOut { get; set; }
        public decimal NetCashFlow { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<CashFlowEntry> Entries { get; set; } = new();
    }
}
