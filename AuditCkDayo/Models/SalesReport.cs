using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum SalesReportStatus
    {
        Uploaded,
        Parsed,
        Draft,
        PendingManagerVerification,
        Confirmed,
        Rejected,
        Adjusted
    }

    public class SalesReport
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DocumentRecordId { get; set; }

        [ForeignKey("DocumentRecordId")]
        public virtual DocumentRecord DocumentRecord { get; set; } = null!;

        [Required]
        public int EstablishmentId { get; set; }

        [ForeignKey("EstablishmentId")]
        public virtual Establishment Establishment { get; set; } = null!;

        public int? CashierUserId { get; set; }

        [ForeignKey("CashierUserId")]
        public virtual User? CashierUser { get; set; }

        [MaxLength(100)]
        public string? CashierName { get; set; }

        [Required]
        public DateTime BusinessDate { get; set; }

        [Required]
        public DateTime HandoverDate { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal GrossSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal CashOut { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal ConfirmedCashToHandover { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal GCashAmount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal CreditAmount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OtherPaymentAmount { get; set; }

        [MaxLength(50)]
        public string? ReceiptNumberStart { get; set; }

        [MaxLength(50)]
        public string? ReceiptNumberEnd { get; set; }

        [MaxLength(100)]
        public string? WitnessName { get; set; }

        [MaxLength(255)]
        public string? Notes { get; set; }

        [Required]
        public SalesReportStatus Status { get; set; } = SalesReportStatus.Draft;

        public int? ConfirmedByUserId { get; set; }

        [ForeignKey("ConfirmedByUserId")]
        public virtual User? ConfirmedByUser { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        public string? ImageUrlsJson { get; set; }

        [NotMapped]
        public List<string> ImageUrls
        {
            get
            {
                if (string.IsNullOrEmpty(ImageUrlsJson))
                {
                    return string.IsNullOrEmpty(DocumentRecord?.ImageUrl) 
                        ? new List<string>() 
                        : new List<string> { DocumentRecord.ImageUrl };
                }
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(ImageUrlsJson) ?? new List<string>();
                }
                catch
                {
                    return string.IsNullOrEmpty(DocumentRecord?.ImageUrl) 
                        ? new List<string>() 
                        : new List<string> { DocumentRecord.ImageUrl };
                }
            }
            set => ImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(value);
        }

        public virtual ICollection<CashBreakdownLine> CashBreakdownLines { get; set; } = new List<CashBreakdownLine>();
    }
}
