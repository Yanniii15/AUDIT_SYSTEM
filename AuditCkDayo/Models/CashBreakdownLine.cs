using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum CashBreakdownOwnerType
    {
        SalesReport,
        AuditSettlement,
        TreasuryCashFlow
    }

    public class CashBreakdownLine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public CashBreakdownOwnerType OwnerType { get; set; }

        [Required]
        public int OwnerId { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Denomination { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Total { get; set; }
    }
}
