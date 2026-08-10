using System.ComponentModel.DataAnnotations;

namespace AuditCkDayo.ViewModels
{
    public class SalesReportReviewViewModel
    {
        public int? SalesReportId { get; set; }
        public int DocumentRecordId { get; set; }

        [Required]
        public int EstablishmentId { get; set; }

        public string? CashierName { get; set; }

        [Required]
        public DateTime BusinessDate { get; set; } = DateTime.Today;

        [Required]
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

        public string? ReceiptNumberStart { get; set; }
        public string? ReceiptNumberEnd { get; set; }
        public string? WitnessName { get; set; }
        public string? Notes { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }
}
