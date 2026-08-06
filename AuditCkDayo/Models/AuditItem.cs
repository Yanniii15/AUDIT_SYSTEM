using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum AuditStatus
    {
        AwaitingBranchVerification,
        AwaitingManagerApproval,
        Approved,
        Rejected,
        Pending,
        AwaitingBranchVerifi = AwaitingBranchVerification,
        AwaitingManagerAppro = AwaitingManagerApproval
    }

    public class AuditItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BuyerId { get; set; }

        [ForeignKey("BuyerId")]
        public virtual User Buyer { get; set; } = null!;

        [Required]
        public int EstablishmentId { get; set; }

        [ForeignKey("EstablishmentId")]
        public virtual Establishment Establishment { get; set; } = null!;

        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime EntryDate { get; set; } = DateTime.Today;

        [Required]
        public AuditStatus Status { get; set; } = AuditStatus.AwaitingBranchVerification;

        public string? Notes { get; set; }

        [MaxLength(255)]
        public string? ReceiptImageUrl { get; set; }

        public int? VerifiedById { get; set; }

        [ForeignKey("VerifiedById")]
        public virtual User? VerifiedBy { get; set; }

        public DateTime? VerificationDate { get; set; }

        public virtual ICollection<AuditItemImage> Images { get; set; } = new List<AuditItemImage>();
        public virtual ICollection<AuditItemDetail> Details { get; set; } = new List<AuditItemDetail>();
    }
}
