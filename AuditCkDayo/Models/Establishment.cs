using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public class Establishment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsOperatingBranch { get; set; } = true;

        public bool IsMiscellaneous { get; set; }

        public bool IsActive { get; set; } = true;

        [Column(TypeName = "decimal(12,2)")]
        public decimal PcfBalance { get; set; } = 0.00m;

        [Column(TypeName = "decimal(12,2)")]
        public decimal DailyStartingFloat { get; set; } = 0.00m;

        public virtual ICollection<AuditItem> AuditItems { get; set; } = new List<AuditItem>();
    }
}
