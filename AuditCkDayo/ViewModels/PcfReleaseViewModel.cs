using System.ComponentModel.DataAnnotations;

namespace AuditCkDayo.ViewModels
{
    public class PcfReleaseViewModel
    {
        public int? ReceiverUserId { get; set; }
        public string? ReceiverName { get; set; }
        public int? EstablishmentId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public DateTime ReleaseDate { get; set; } = DateTime.Today;

        public string? Purpose { get; set; }
    }
}
