using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum PcfReleaseStatus
    {
        Released,
        PartiallyAudited,
        Settled,
        Cancelled
    }

    public class PcfRelease
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ReleasedByTreasuryUserId { get; set; }

        [ForeignKey("ReleasedByTreasuryUserId")]
        public virtual User ReleasedByTreasuryUser { get; set; } = null!;

        public int? ReceiverUserId { get; set; }

        [ForeignKey("ReceiverUserId")]
        public virtual User? ReceiverUser { get; set; }

        [MaxLength(100)]
        public string? ReceiverName { get; set; }

        public int? EstablishmentId { get; set; }

        [ForeignKey("EstablishmentId")]
        public virtual Establishment? Establishment { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime ReleaseDate { get; set; } = DateTime.UtcNow;

        [MaxLength(255)]
        public string? Purpose { get; set; }

        [Required]
        public PcfReleaseStatus Status { get; set; } = PcfReleaseStatus.Released;

        public int? CashFlowEntryId { get; set; }

        [ForeignKey("CashFlowEntryId")]
        public virtual CashFlowEntry? CashFlowEntry { get; set; }
    }
}
