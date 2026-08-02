using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
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
    }
}
