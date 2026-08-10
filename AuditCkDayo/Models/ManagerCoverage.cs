using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    [Flags]
    public enum CoverageScope
    {
        None = 0,
        SalesReports = 1,
        TreasuryCashIn = 2,
        PcfRelease = 4,
        AuditSettlement = 8,
        BuyerAudits = 16,
        BranchHandovers = 32,
        All = SalesReports | TreasuryCashIn | PcfRelease | AuditSettlement | BuyerAudits | BranchHandovers
    }

    public class ManagerCoverage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CoveredManagerId { get; set; }

        [ForeignKey("CoveredManagerId")]
        public virtual User CoveredManager { get; set; } = null!;

        [Required]
        public int CoveringManagerId { get; set; }

        [ForeignKey("CoveringManagerId")]
        public virtual User CoveringManager { get; set; } = null!;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public CoverageScope Scope { get; set; } = CoverageScope.All;

        [MaxLength(255)]
        public string? Reason { get; set; }

        [Required]
        public int CreatedByUserId { get; set; }

        [ForeignKey("CreatedByUserId")]
        public virtual User CreatedByUser { get; set; } = null!;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public bool CoversDate(DateTime date)
        {
            var day = date.Date;
            return IsActive && StartDate.Date <= day && EndDate.Date >= day;
        }
    }
}
