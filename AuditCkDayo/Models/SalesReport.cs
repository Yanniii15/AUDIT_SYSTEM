using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum SalesReportStatus
    {
        Uploaded,
        Parsed,
        Draft,
        PendingManagerVerification,
        Confirmed,
        Rejected,
        Adjusted
    }

    public enum SalesReportSection
    {
        Closing = 0,
        Opening = 1
    }

    public class SalesReport
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DocumentRecordId { get; set; }

        [ForeignKey("DocumentRecordId")]
        public virtual DocumentRecord DocumentRecord { get; set; } = null!;

        [Required]
        public int EstablishmentId { get; set; }

        [ForeignKey("EstablishmentId")]
        public virtual Establishment Establishment { get; set; } = null!;

        public int? CashierUserId { get; set; }

        [ForeignKey("CashierUserId")]
        public virtual User? CashierUser { get; set; }

        public int? OpeningInputtedByUserId { get; set; }

        [ForeignKey("OpeningInputtedByUserId")]
        public virtual User? OpeningInputtedByUser { get; set; }

        public int? ClosingInputtedByUserId { get; set; }

        [ForeignKey("ClosingInputtedByUserId")]
        public virtual User? ClosingInputtedByUser { get; set; }

        [MaxLength(100)]
        public string? CashierName { get; set; }

        [Required]
        public DateTime BusinessDate { get; set; }

        [Required]
        public DateTime HandoverDate { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal GrossSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal CashOut { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal ConfirmedCashToHandover { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal GCashAmount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal CreditAmount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OtherPaymentAmount { get; set; }

        [MaxLength(50)]
        public string? ReceiptNumberStart { get; set; }

        [MaxLength(50)]
        public string? ReceiptNumberEnd { get; set; }

        [MaxLength(100)]
        public string? WitnessName { get; set; }

        [MaxLength(255)]
        public string? Notes { get; set; }

        [Required]
        public SalesReportStatus Status { get; set; } = SalesReportStatus.Draft;

        public int? ConfirmedByUserId { get; set; }

        [ForeignKey("ConfirmedByUserId")]
        public virtual User? ConfirmedByUser { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        public string? ImageUrlsJson { get; set; }
        public string? ClosingImageUrlsJson { get; set; }

        [NotMapped]
        public List<string> ImageUrls
        {
            get
            {
                if (string.IsNullOrEmpty(ImageUrlsJson))
                {
                    return string.IsNullOrEmpty(DocumentRecord?.ImageUrl) 
                        ? new List<string>() 
                        : new List<string> { DocumentRecord.ImageUrl };
                }
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(ImageUrlsJson) ?? new List<string>();
                }
                catch
                {
                    return string.IsNullOrEmpty(DocumentRecord?.ImageUrl) 
                        ? new List<string>() 
                        : new List<string> { DocumentRecord.ImageUrl };
                }
            }
            set => ImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public List<string> ClosingImageUrls
        {
            get
            {
                if (string.IsNullOrEmpty(ClosingImageUrlsJson))
                {
                    return new List<string>();
                }
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(ClosingImageUrlsJson) ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
            set => ClosingImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(value);
        }

        [Column(TypeName = "decimal(12,2)")]
        public decimal ClosingGrossSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal FoodSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal BeerSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal BeverageSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal HardSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OtherSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal CashSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal SeniorDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal PwdDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal LoyaltyCardDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal GiftVoucherDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal EmployeeTenPercentDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal EmployeeFivePercentDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal EaglesDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal SalesShortageAmount { get; set; }

        [MaxLength(255)]
        public string? SalesShortageReason { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal SalesOverageAmount { get; set; }

        [MaxLength(255)]
        public string? SalesOverageReason { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal RestoPcf { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal PcfFromSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal ChangeAmount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningGrossSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningCashSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningFoodSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningBeerSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningBeverageSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningHardSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningOtherSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningSeniorDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningPwdDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningLoyaltyCardDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningGiftVoucherDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningEmployeeTenPercentDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningEmployeeFivePercentDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningEaglesDiscount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningSalesShortageAmount { get; set; }

        [MaxLength(255)]
        public string? OpeningSalesShortageReason { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningSalesOverageAmount { get; set; }

        [MaxLength(255)]
        public string? OpeningSalesOverageReason { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningRestoPcf { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningPcfFromSales { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpeningChangeAmount { get; set; }

        [MaxLength(50)]
        public string? OpeningReceiptNumberStart { get; set; }

        [MaxLength(50)]
        public string? OpeningReceiptNumberEnd { get; set; }

        [MaxLength(100)]
        public string? OpeningWitnessName { get; set; }

        [MaxLength(255)]
        public string? OpeningNotes { get; set; }

        // --- Bank Deposit Slip Verification Fields ---
        [MaxLength(255)]
        public string? DepositSlipImageUrl { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? DepositedAmount { get; set; }

        [MaxLength(100)]
        public string? DepositBankName { get; set; }

        [MaxLength(100)]
        public string? DepositReferenceNumber { get; set; }

        public DateTime? DepositDate { get; set; }

        [MaxLength(500)]
        public string? DepositVarianceReason { get; set; }

        public int? DepositUploadedByUserId { get; set; }

        [ForeignKey("DepositUploadedByUserId")]
        public virtual User? DepositUploadedByUser { get; set; }

        public DateTime? DepositUploadedAt { get; set; }

        [NotMapped]
        public bool HasDepositSlip => !string.IsNullOrWhiteSpace(DepositSlipImageUrl);

        [NotMapped]
        public decimal DepositVariance => (DepositedAmount ?? 0m) - ConfirmedCashToHandover;

        [NotMapped]
        public bool IsDepositMatched => HasDepositSlip && Math.Abs(DepositVariance) < 0.01m;

        [NotMapped]
        public bool IsDepositDiscrepancy => HasDepositSlip && Math.Abs(DepositVariance) >= 0.01m;

        [NotMapped]
        public decimal TotalGrossSales => ClosingGrossSales > 0m ? OpeningGrossSales + ClosingGrossSales : GrossSales;

        [NotMapped]
        public decimal TotalCashSales => CashSales + OpeningCashSales;

        [NotMapped]
        public decimal TotalConfirmedCashToHandover => ConfirmedCashToHandover + OpeningCashSales;

        [NotMapped]
        public string OpeningInputtedByDisplayName => ResolveInputtedByDisplayName(OpeningInputtedByUser, DocumentRecord?.UploadedByUser, CashierName);

        [NotMapped]
        public string ClosingInputtedByDisplayName => ResolveInputtedByDisplayName(ClosingInputtedByUser, DocumentRecord?.UploadedByUser, CashierName);

        [NotMapped]
        public string InputtedBySummary
        {
            get
            {
                var openingName = OpeningInputtedByDisplayName;
                var closingName = ClosingInputtedByDisplayName;
                return string.Equals(openingName, closingName, StringComparison.Ordinal)
                    ? openingName
                    : $"Opening: {openingName} / Closing: {closingName}";
            }
        }

        private static string ResolveInputtedByDisplayName(User? sectionInputter, User? documentUploader, string? legacyCashierName)
        {
            if (!string.IsNullOrWhiteSpace(sectionInputter?.Name))
            {
                return sectionInputter.Name;
            }
            if (!string.IsNullOrWhiteSpace(documentUploader?.Name))
            {
                return documentUploader.Name;
            }
            return string.IsNullOrWhiteSpace(legacyCashierName) ? "—" : legacyCashierName;
        }

        public virtual ICollection<SalesReportLine> Lines { get; set; } = new List<SalesReportLine>();

        public virtual ICollection<CashBreakdownLine> CashBreakdownLines { get; set; } = new List<CashBreakdownLine>();

    }
}
