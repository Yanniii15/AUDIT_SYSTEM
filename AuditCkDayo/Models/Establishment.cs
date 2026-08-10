using System.ComponentModel.DataAnnotations;

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

        public virtual ICollection<AuditItem> AuditItems { get; set; } = new List<AuditItem>();
    }
}
