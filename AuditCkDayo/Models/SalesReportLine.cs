using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum SalesReportLineType
    {
        GCash,
        BankTransfer,
        Card,
        Credit,
        RunawayCustomer,
        ExpenseFromSales
    }

    public class SalesReportLine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SalesReportId { get; set; }

        [ForeignKey("SalesReportId")]
        public virtual SalesReport SalesReport { get; set; } = null!;

        [Required]
        public SalesReportLineType LineType { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [MaxLength(100)]
        public string? Label { get; set; }

        public int SortOrder { get; set; }

        public SalesReportSection Section { get; set; } = SalesReportSection.Closing;
    }
}
