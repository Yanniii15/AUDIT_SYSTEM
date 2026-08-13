using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels
{
    public class PcfMonitorViewModel
    {
        public List<User> Users { get; set; } = new List<User>();
        public string ScopeLabel { get; set; } = string.Empty;
        public decimal TotalStartingPcf => Users.Sum(u => u.DailyStartingFloat);
        public decimal TotalCurrentPcf => Users.Sum(u => u.PcfBalance);
        public decimal TotalUsedPcf => TotalStartingPcf - TotalCurrentPcf;
    }
}
