using System.ComponentModel.DataAnnotations;

namespace AuditCkDayo.Models
{
    public enum CostCenterCategory
    {
        Operations,
        Cater,
        Comm,
        Payroll,
        Utilities,
        StaffMeal,
        Vehicle,
        PersonTag,
        Miscellaneous,
        Others
    }

    public class CostCenter
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public CostCenterCategory Category { get; set; } = CostCenterCategory.Others;

        public bool IsActive { get; set; } = true;

        [MaxLength(255)]
        public string? Notes { get; set; }
    }
}
