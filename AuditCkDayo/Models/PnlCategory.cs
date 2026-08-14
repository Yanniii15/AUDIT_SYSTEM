using System.ComponentModel.DataAnnotations;

namespace AuditCkDayo.Models
{
    public class PnlCategory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public PnlExpenseSection Section { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
