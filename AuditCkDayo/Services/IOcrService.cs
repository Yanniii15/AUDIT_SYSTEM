using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuditCkDayo.Services
{
    public class OcrItemResult
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }

    public class OcrResult
    {
        public decimal TotalAmount { get; set; }
        public DateTime? TransactionDate { get; set; }
        public List<OcrItemResult> Items { get; set; } = new();
    }

    public interface IOcrService
    {
        Task<OcrResult> ParseReceiptAsync(Stream imageStream);
    }
}
