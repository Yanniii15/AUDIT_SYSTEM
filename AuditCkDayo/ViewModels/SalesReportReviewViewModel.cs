using System.ComponentModel.DataAnnotations;

namespace AuditCkDayo.ViewModels
{
    public class SalesReportReviewViewModel
    {
        public int? SalesReportId { get; set; }
        public int DocumentRecordId { get; set; }

        [Required]
        public int EstablishmentId { get; set; }

        [StringLength(100)]
        public string? CashierName { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime BusinessDate { get; set; } = DateTime.Today;

        [Required]
        [DataType(DataType.Date)]
        public DateTime HandoverDate { get; set; } = DateTime.Today;

        [Range(0, double.MaxValue)]
        public decimal GrossSales { get; set; }

        [Range(0, double.MaxValue)]
        public decimal CashOut { get; set; }

        [Range(0, double.MaxValue)]
        public decimal ConfirmedCashToHandover { get; set; }

        [Range(0, double.MaxValue)]
        public decimal GCashAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal CreditAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal OtherPaymentAmount { get; set; }

        [StringLength(50)]
        public string? ReceiptNumberStart { get; set; }
        [StringLength(50)]
        public string? ReceiptNumberEnd { get; set; }
        [StringLength(100)]
        public string? WitnessName { get; set; }
        [StringLength(255)]
        public string? Notes { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public List<string>? ImageUrls { get; set; }
        public List<CashBreakdownLineViewModel> Items { get; set; } = new();
    }

    public class CashBreakdownLineViewModel
    {
        public int Id { get; set; }
        public decimal Denomination { get; set; }
        public int Quantity { get; set; }
        public decimal Total { get; set; }
    }
}
