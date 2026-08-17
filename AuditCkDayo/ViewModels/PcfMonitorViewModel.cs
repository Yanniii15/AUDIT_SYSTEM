using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels
{
    /// <summary>
    /// A single PCF monitor row: either an establishment (sharing a branch fund
    /// among its BranchStaff) or an individual holder such as a Buyer (personal fund).
    /// </summary>
    public class PcfMonitorItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string EstablishmentName { get; set; } = "—";
        public decimal StartingPcf { get; set; }
        public decimal CurrentPcf { get; set; }
        public decimal UsedPcf => StartingPcf - CurrentPcf;
        public decimal Utilization => StartingPcf <= 0 ? 0 : Math.Round((UsedPcf / StartingPcf) * 100, 1);
    }

    public class PcfMonitorViewModel
    {
        public string ScopeLabel { get; set; } = string.Empty;
        public List<PcfMonitorItem> Items { get; set; } = new List<PcfMonitorItem>();
        public decimal TotalStartingPcf => Items.Sum(g => g.StartingPcf);
        public decimal TotalCurrentPcf => Items.Sum(g => g.CurrentPcf);
        public decimal TotalUsedPcf => TotalStartingPcf - TotalCurrentPcf;
    }
}