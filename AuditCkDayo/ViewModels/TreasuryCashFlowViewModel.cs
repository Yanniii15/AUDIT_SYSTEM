using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels
{
    public class TreasuryCashFlowViewModel
    {
        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public DateTime CashFlowDate
        {
            get => SelectedDate;
            set => SelectedDate = value;
        }
        public int? FlowId { get; set; }
        public TreasuryCashFlowStatus? Status { get; set; }
        public decimal StartingBalance { get; set; }
        public decimal TotalCashIn { get; set; }
        public decimal TotalCashOut { get; set; }
        public decimal NetCashFlow { get; set; }
        public decimal ClosingBalance { get; set; }
        public List<CashFlowEntry> Entries { get; set; } = new();
    }
}
