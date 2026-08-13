using System.ComponentModel.DataAnnotations;
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
        public ManualCashInViewModel ManualCashIn { get; set; } = new();
        public ManualCashOutViewModel ManualCashOut { get; set; } = new();
    }

    public class ManualCashInViewModel
    {
        [DataType(DataType.Date)]
        public DateTime CashInDate { get; set; } = DateTime.Today;

        [Required]
        public CashFlowCategory Category { get; set; } = CashFlowCategory.Others;

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        [MaxLength(255)]
        public string? Purpose { get; set; }
    }

    public class ManualCashOutViewModel
    {
        [DataType(DataType.Date)]
        public DateTime CashOutDate { get; set; } = DateTime.Today;

        [Required]
        public CashFlowCategory Category { get; set; } = CashFlowCategory.Others;

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        public int? EstablishmentId { get; set; }

        [MaxLength(255)]
        public string? Purpose { get; set; }

        public bool SplitAcrossEstablishments { get; set; }

        public List<ManualCashOutSplitViewModel> SplitRows { get; set; } = new();
    }

    public class ManualCashOutSplitViewModel
    {
        public int? EstablishmentId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }
    }
}
