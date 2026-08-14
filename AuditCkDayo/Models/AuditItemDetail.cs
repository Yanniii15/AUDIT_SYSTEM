using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum ReceiptLineStatus
    {
        HasReceipt,
        NoReceipt,
        NotRequired
    }

    public enum BranchVerificationStatus
    {
        Pending,
        Verified,
        Rejected
    }

    public class AuditItemDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AuditItemId { get; set; }

        [ForeignKey("AuditItemId")]
        public virtual AuditItem AuditItem { get; set; } = null!;

        [Required]
        [MaxLength(150)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        public int Quantity { get; set; } = 1;

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Price { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Total { get; set; }

        public int? AssignedEstablishmentId { get; set; }

        [ForeignKey("AssignedEstablishmentId")]
        public virtual Establishment? AssignedEstablishment { get; set; }

        public int? CostCenterId { get; set; }

        [ForeignKey("CostCenterId")]
        public virtual CostCenter? CostCenter { get; set; }

        [Required]
        public ReceiptLineStatus ReceiptStatus { get; set; } = ReceiptLineStatus.HasReceipt;

        [Required]
        public BranchVerificationStatus BranchVerificationStatus { get; set; } = BranchVerificationStatus.Pending;

        [MaxLength(255)]
        public string? AllocationNotes { get; set; }

        public int? PnlCategoryId { get; set; }

        [ForeignKey("PnlCategoryId")]
        public virtual PnlCategory? PnlCategory { get; set; }

        [Required]
        public PnlExpenseSection PnlSection { get; set; } = PnlExpenseSection.Other;

        [Required]
        [MaxLength(100)]
        public string PnlCategoryName { get; set; } = "Other";
    }
}
