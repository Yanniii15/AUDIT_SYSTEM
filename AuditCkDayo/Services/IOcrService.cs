using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuditCkDayo.Services
{
    public class OcrItemResult
    {
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1m;
        public decimal Price { get; set; }
        public decimal Total { get; set; }
        public int? AssignedEstablishmentId { get; set; }
        public int? CostCenterId { get; set; }
        public string? CombinedDestinationId { get; set; }
        public string? AllocationNotes { get; set; }
        public int? ExpenseSourceId { get; set; }
        public string? ExpenseSourceName { get; set; }
        public int? PnlCategoryId { get; set; }
        public AuditCkDayo.Models.PnlExpenseSection PnlSection { get; set; } = AuditCkDayo.Models.PnlExpenseSection.Other;
        public string PnlCategoryName { get; set; } = "Other";
        public AuditCkDayo.Models.ReceiptLineStatus ReceiptStatus { get; set; } = AuditCkDayo.Models.ReceiptLineStatus.HasReceipt;
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

    public class SalesReportOcrPaymentLine
    {
        public string? Label { get; set; }
        public decimal Amount { get; set; }
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
        public List<SalesReportOcrPaymentLine> GCashLines { get; set; } = new();
        public List<SalesReportOcrPaymentLine> BankTransferLines { get; set; } = new();
        public List<SalesReportOcrPaymentLine> CardLines { get; set; } = new();
        public List<SalesReportOcrPaymentLine> CreditLines { get; set; } = new();
        public List<SalesReportOcrPaymentLine> RunawayCustomerLines { get; set; } = new();
        public List<SalesReportOcrPaymentLine> ExpenseFromSalesLines { get; set; } = new();
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
