using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public class AuditItemImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AuditItemId { get; set; }

        [ForeignKey("AuditItemId")]
        public virtual AuditItem AuditItem { get; set; } = null!;

        [Required]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        [Required]
        public int DisplayOrder { get; set; }
    }
}
