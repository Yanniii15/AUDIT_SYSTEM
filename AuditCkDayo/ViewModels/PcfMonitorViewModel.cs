using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels
{
    public class PcfEstablishmentGroup
    {
        public int EstablishmentId { get; set; }
        public string EstablishmentName { get; set; } = "Unassigned";
        public decimal SharedStartingPcf { get; set; }
        public decimal SharedCurrentPcf { get; set; }
        public decimal SharedUsedPcf => SharedStartingPcf - SharedCurrentPcf;
        public decimal Utilization => SharedStartingPcf <= 0 ? 0 : Math.Round((SharedUsedPcf / SharedStartingPcf) * 100, 1);
        public List<User> Staff { get; set; } = new List<User>();
    }

    public class PcfMonitorViewModel
    {
        public string ScopeLabel { get; set; } = string.Empty;
        public List<PcfEstablishmentGroup> Establishments { get; set; } = new List<PcfEstablishmentGroup>();
        public int AccountCount => Establishments.Sum(g => g.Staff.Count);
        public decimal TotalStartingPcf => Establishments.Sum(g => g.SharedStartingPcf);
        public decimal TotalCurrentPcf => Establishments.Sum(g => g.SharedCurrentPcf);
        public decimal TotalUsedPcf => TotalStartingPcf - TotalCurrentPcf;
    }
}
