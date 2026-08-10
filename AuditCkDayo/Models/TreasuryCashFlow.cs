using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum TreasuryCashFlowStatus
    {
        Draft,
        Open,
        Closed
    }

    public class TreasuryCashFlow
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TreasuryUserId { get; set; }

        [ForeignKey("TreasuryUserId")]
        public virtual User TreasuryUser { get; set; } = null!;

        [Required]
        public DateTime CashFlowDate { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal StartingBalance { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalCashIn { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalCashOut { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal NetCashFlow { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal ClosingBalance { get; set; }

        [Required]
        public TreasuryCashFlowStatus Status { get; set; } = TreasuryCashFlowStatus.Open;

        public virtual ICollection<CashFlowEntry> Entries { get; set; } = new List<CashFlowEntry>();

        public void RecomputeTotals()
        {
            TotalCashIn = Entries.Where(e => e.Direction == CashFlowDirection.In).Sum(e => e.Amount);
            TotalCashOut = Entries.Where(e => e.Direction == CashFlowDirection.Out).Sum(e => e.Amount);
            NetCashFlow = StartingBalance + TotalCashIn;
            ClosingBalance = NetCashFlow - TotalCashOut;
        }
    }
}
