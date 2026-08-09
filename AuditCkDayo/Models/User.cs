using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum UserRole
    {
        Owner,
        Admin,
        Manager,
        Buyer,
        BranchStaff
    }

    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        public int? ManagerId { get; set; }

        [ForeignKey("ManagerId")]
        public virtual User? Manager { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal PcfBalance { get; set; } = 0.00m;

        [Column(TypeName = "decimal(12,2)")]
        public decimal DailyStartingFloat { get; set; } = 0.00m;

        public bool IsDeleted { get; set; }


        public int? EstablishmentId { get; set; }

        [ForeignKey("EstablishmentId")]
        public virtual Establishment? Establishment { get; set; }
        public virtual ICollection<User> StaffMembers { get; set; } = new List<User>();
        public virtual ICollection<AuditItem> AuditItems { get; set; } = new List<AuditItem>();
    }
}
