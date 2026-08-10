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
        public int? AssignedEstablishmentId { get; set; }
        public int? CostCenterId { get; set; }
        public string? CombinedDestinationId { get; set; }
    }

    public class OcrResult
    {
        public decimal TotalAmount { get; set; }
        public DateTime? TransactionDate { get; set; }
        public List<OcrItemResult> Items { get; set; } = new();
    }

    public class DenominationOcrResult
    {
        public decimal Denomination { get; set; }
        public int Quantity { get; set; }
    }

    public class SalesReportOcrResult
    {
        public string? CashierName { get; set; }
        public DateTime? BusinessDate { get; set; }
        public decimal GrossSales { get; set; }
        public decimal CashOut { get; set; }
        public decimal ConfirmedCashToHandover { get; set; }
        public decimal GCashAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal OtherPaymentAmount { get; set; }
        public string? ReceiptNumberStart { get; set; }
        public string? ReceiptNumberEnd { get; set; }
        public string? WitnessName { get; set; }
        public List<DenominationOcrResult> Denominations { get; set; } = new();
        public string? RawJson { get; set; }
    }

    public interface IOcrService
    {
        Task<OcrResult> ParseReceiptAsync(List<Stream> imageStreams);
        Task<SalesReportOcrResult> ParseSalesReportAsync(Stream imageStream);
    }
}
