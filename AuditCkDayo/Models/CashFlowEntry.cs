using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum CashFlowDirection
    {
        In,
        Out
    }

    public enum CashFlowCategory
    {
        Sales,
        ChangePcf,
        OwnerFunding,
        PcfRelease,
        CashAdvance,
        Expense,
        Payroll,
        Utilities,
        Supplier,
        Others
    }

    public class CashFlowEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TreasuryCashFlowId { get; set; }

        [ForeignKey("TreasuryCashFlowId")]
        public virtual TreasuryCashFlow TreasuryCashFlow { get; set; } = null!;

        [Required]
        public CashFlowDirection Direction { get; set; }

        [Required]
        public CashFlowCategory Category { get; set; }

        public int? EstablishmentId { get; set; }

        [ForeignKey("EstablishmentId")]
        public virtual Establishment? Establishment { get; set; }

        public int? CostCenterId { get; set; }

        [ForeignKey("CostCenterId")]
        public virtual CostCenter? CostCenter { get; set; }

        public int? RelatedUserId { get; set; }

        [ForeignKey("RelatedUserId")]
        public virtual User? RelatedUser { get; set; }

        public int? SourceDocumentId { get; set; }

        [ForeignKey("SourceDocumentId")]
        public virtual DocumentRecord? SourceDocument { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [MaxLength(255)]
        public string? Notes { get; set; }

        [Required]
        public int CreatedByUserId { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual User CreatedByUser { get; set; } = null!;

        public int? ConfirmedByUserId { get; set; }

        [ForeignKey("ConfirmedByUserId")]
        public virtual User? ConfirmedByUser { get; set; }
    }
}
