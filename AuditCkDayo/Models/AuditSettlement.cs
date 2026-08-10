using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum AuditSettlementStatus
    {
        Draft,
        Submitted,
        UnderReview,
        Confirmed,
        Rejected,
        Adjusted
    }

    public class AuditSettlement
    {
        [Key]
        public int Id { get; set; }

        public int? PcfReleaseId { get; set; }

        [ForeignKey("PcfReleaseId")]
        public virtual PcfRelease? PcfRelease { get; set; }

        public int? ReceiverUserId { get; set; }

        [ForeignKey("ReceiverUserId")]
        public virtual User? ReceiverUser { get; set; }

        [MaxLength(100)]
        public string? ReceiverName { get; set; }

        [Required]
        public int ResponsibleManagerId { get; set; }

        [ForeignKey("ResponsibleManagerId")]
        public virtual User ResponsibleManager { get; set; } = null!;

        [Required]
        public int ProcessedByUserId { get; set; }

        [ForeignKey("ProcessedByUserId")]
        public virtual User ProcessedByUser { get; set; } = null!;

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalPCReleased { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalAcceptedExpenses { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal ExpectedChange { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal ActualChangeReturned { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal ShortOverAmount { get; set; }

        [Required]
        public AuditSettlementStatus Status { get; set; } = AuditSettlementStatus.Draft;

        public void Recompute()
        {
            ExpectedChange = TotalPCReleased - TotalAcceptedExpenses;
            ShortOverAmount = ActualChangeReturned - ExpectedChange;
        }
    }
}
