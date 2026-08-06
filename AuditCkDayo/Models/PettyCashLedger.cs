using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum LedgerTransactionType
    {
        VaultFunding,
        ManagerFunding,
        ExpenseDeduction,
        ReversalRefund,
        CashSurrender,
        ManualAdjustment
    }

    public class PettyCashLedger
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [Required]
        public LedgerTransactionType TransactionType { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal ResultingBalance { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        public int? AssociatedRecordId { get; set; }

        public int? CounterpartyUserId { get; set; }

        [ForeignKey("CounterpartyUserId")]
        public virtual User? CounterpartyUser { get; set; }

        [MaxLength(255)]
        public string? Notes { get; set; }
    }
}
