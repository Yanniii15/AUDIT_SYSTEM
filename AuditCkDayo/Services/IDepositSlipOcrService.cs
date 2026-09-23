using System;
using System.IO;
using System.Threading.Tasks;

namespace AuditCkDayo.Services
{
    public class DepositSlipOcrResult
    {
        public bool Success { get; set; }
        public string? DetectedBank { get; set; }
        public decimal? DetectedAmount { get; set; }
        public string? DetectedReference { get; set; }
        public DateTime? DetectedDate { get; set; }
        public string? RawText { get; set; }
    }

    public interface IDepositSlipOcrService
    {
        Task<DepositSlipOcrResult> ParseDepositSlipAsync(Stream imageStream);
    }
}
