using System.ComponentModel.DataAnnotations;
using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels
{
    public class UserEditViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Role")]
        public UserRole Role { get; set; }

        [Display(Name = "Manager")]
        public int? ManagerId { get; set; }

        [Display(Name = "Establishment")]
        public int? EstablishmentId { get; set; }

        [Display(Name = "Treasury User")]
        public bool IsTreasury { get; set; }
    }
}
