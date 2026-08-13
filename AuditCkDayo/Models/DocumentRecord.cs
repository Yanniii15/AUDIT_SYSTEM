using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum DocumentType
    {
        ExpenseReceipt,
        DailySalesReport,
        AuditSheet
    }

    public enum OcrStatus
    {
        NotStarted,
        Parsed,
        Failed
    }

    public enum DocumentReviewStatus
    {
        Uploaded,
        Draft,
        PendingManagerVerification,
        Confirmed,
        Rejected
    }

    public class DocumentRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DocumentType DocumentType { get; set; }

        [Required]
        public int UploadedByUserId { get; set; }

        [ForeignKey("UploadedByUserId")]
        public virtual User UploadedByUser { get; set; } = null!;

        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(255)]
        public string ImageUrl { get; set; } = string.Empty;

        public string? OcrRawJson { get; set; }

        [Required]
        public OcrStatus OcrStatus { get; set; } = OcrStatus.NotStarted;

        [Required]
        public DocumentReviewStatus ReviewStatus { get; set; } = DocumentReviewStatus.Uploaded;

        public int? ConfirmedByUserId { get; set; }

        [ForeignKey("ConfirmedByUserId")]
        public virtual User? ConfirmedByUser { get; set; }

        public DateTime? ConfirmedAt { get; set; }
    }
}
