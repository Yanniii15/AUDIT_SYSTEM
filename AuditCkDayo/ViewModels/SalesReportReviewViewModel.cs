using AuditCkDayo.Models;
using System.ComponentModel.DataAnnotations;

namespace AuditCkDayo.ViewModels
{
    public class SalesReportReviewViewModel
    {
        public int? SalesReportId { get; set; }
        public int DocumentRecordId { get; set; }

        [Required]
        public int EstablishmentId { get; set; }

        [StringLength(100)]
        public string? CashierName { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime BusinessDate { get; set; } = DateTime.Today;

        [Required]
        [DataType(DataType.Date)]
        public DateTime HandoverDate { get; set; } = DateTime.Today;

        [Range(0, double.MaxValue)]
        public decimal GrossSales { get; set; }

        [Range(0, double.MaxValue)]
        public decimal CashOut { get; set; }

        [Range(0, double.MaxValue)]
        public decimal ConfirmedCashToHandover { get; set; }

        [Range(0, double.MaxValue)]
        public decimal ManagerCountedTotalCash { get; set; }

        [Range(0, double.MaxValue)]
        public decimal GCashAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal CreditAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal OtherPaymentAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal ClosingGrossSales { get; set; }

        [Range(0, double.MaxValue)]
        public decimal FoodSales { get; set; }

        [Range(0, double.MaxValue)]
        public decimal BeerSales { get; set; }

        [Range(0, double.MaxValue)]
        public decimal BeverageSales { get; set; }

        [Range(0, double.MaxValue)]
        public decimal OtherSales { get; set; }

        [Range(0, double.MaxValue)]
        public decimal CashSales { get; set; }

        [Range(0, double.MaxValue)]
        public decimal SeniorDiscount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PwdDiscount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal LoyaltyCardDiscount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal GiftVoucherDiscount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal EmployeeTenPercentDiscount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal EmployeeFivePercentDiscount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal EaglesDiscount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal SalesShortageAmount { get; set; }

        [StringLength(255)]
        public string? SalesShortageReason { get; set; }

        [Range(0, double.MaxValue)]
        public decimal SalesOverageAmount { get; set; }

        [StringLength(255)]
        public string? SalesOverageReason { get; set; }

        [Range(0, double.MaxValue)]
        public decimal RestoPcf { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PcfFromSales { get; set; }

        [Range(0, double.MaxValue)]
        public decimal ChangeAmount { get; set; }

        public SalesReportSection ReportSection { get; set; } = SalesReportSection.Closing;

        [Range(0, double.MaxValue)]
        public decimal OpeningGrossSales { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningCashSales { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningFoodSales { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningBeerSales { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningBeverageSales { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningOtherSales { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningSeniorDiscount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningPwdDiscount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningLoyaltyCardDiscount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningGiftVoucherDiscount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningEmployeeTenPercentDiscount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningEmployeeFivePercentDiscount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningEaglesDiscount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningSalesShortageAmount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningSalesOverageAmount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningRestoPcf { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningPcfFromSales { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningChangeAmount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningGCashAmount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningCreditAmount { get; set; }
        [Range(0, double.MaxValue)]
        public decimal OpeningOtherPaymentAmount { get; set; }

        [StringLength(255)]
        public string? OpeningSalesShortageReason { get; set; }
        [StringLength(255)]
        public string? OpeningSalesOverageReason { get; set; }
        [StringLength(50)]
        public string? OpeningReceiptNumberStart { get; set; }
        [StringLength(50)]
        public string? OpeningReceiptNumberEnd { get; set; }
        [StringLength(100)]
        public string? OpeningWitnessName { get; set; }
        [StringLength(255)]
        public string? OpeningNotes { get; set; }

        public List<SalesReportLineViewModel> GCashLines { get; set; } = new();
        public List<SalesReportLineViewModel> BankTransferLines { get; set; } = new();
        public List<SalesReportLineViewModel> CardLines { get; set; } = new();
        public List<SalesReportLineViewModel> CreditLines { get; set; } = new();
        public List<SalesReportLineViewModel> RunawayCustomerLines { get; set; } = new();
        public List<SalesReportLineViewModel> ExpenseFromSalesLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningGCashLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningBankTransferLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningCardLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningCreditLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningRunawayCustomerLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningExpenseFromSalesLines { get; set; } = new();

        public decimal TotalGCash => GCashLines.Any() ? GCashLines.Sum(l => l.Amount) : GCashAmount;
        public decimal TotalBankTransfer => BankTransferLines.Sum(l => l.Amount);
        public decimal TotalCard => CardLines.Sum(l => l.Amount);
        public decimal TotalCredit => CreditLines.Any() ? CreditLines.Sum(l => l.Amount) : CreditAmount;
        public decimal TotalRunawayCustomer => RunawayCustomerLines.Sum(l => l.Amount);
        public decimal TotalExpensesFromSales => ExpenseFromSalesLines.Sum(l => l.Amount);
        public decimal OpeningTotalGCash => OpeningGCashLines.Any() ? OpeningGCashLines.Sum(l => l.Amount) : OpeningGCashAmount;
        public decimal OpeningTotalBankTransfer => OpeningBankTransferLines.Sum(l => l.Amount);
        public decimal OpeningTotalCard => OpeningCardLines.Sum(l => l.Amount);
        public decimal OpeningTotalCredit => OpeningCreditLines.Any() ? OpeningCreditLines.Sum(l => l.Amount) : OpeningCreditAmount;
        public decimal OpeningTotalRunawayCustomer => OpeningRunawayCustomerLines.Sum(l => l.Amount);
        public decimal OpeningTotalExpensesFromSales => OpeningExpenseFromSalesLines.Sum(l => l.Amount);

        public decimal TotalDiscounts => SeniorDiscount + PwdDiscount + LoyaltyCardDiscount + GiftVoucherDiscount + EmployeeTenPercentDiscount + EmployeeFivePercentDiscount + EaglesDiscount;

        public decimal ExpectedCashToHandover
        {
            get
            {
                decimal nonCash = TotalGCash + TotalCredit;
                decimal otherPayments = TotalBankTransfer + TotalCard + TotalRunawayCustomer;
                if (otherPayments == 0m)
                {
                    otherPayments = OtherPaymentAmount;
                }
                return GrossSales - nonCash - otherPayments;
            }
        }
        public decimal ShortOverAmount => ConfirmedCashToHandover - ExpectedCashToHandover;
        public string ShortOverLabel => ShortOverAmount < 0 ? "Short" : ShortOverAmount > 0 ? "Over" : "Balanced";

        public decimal CombinedExpectedCashToHandover => ExpectedCashToHandover + OpeningExpectedCashToHandover;
        public decimal CombinedShortOverAmount => ConfirmedCashToHandover - CombinedExpectedCashToHandover;
        public string CombinedShortOverLabel => CombinedShortOverAmount < 0 ? "Short" : CombinedShortOverAmount > 0 ? "Over" : "Balanced";

        public decimal CombinedGrossSales => GrossSales + OpeningGrossSales;
        public decimal CombinedFoodSales => FoodSales + OpeningFoodSales;
        public decimal CombinedBeerSales => BeerSales + OpeningBeerSales;
        public decimal CombinedBeverageSales => BeverageSales + OpeningBeverageSales;
        public decimal CombinedOtherSales => OtherSales + OpeningOtherSales;
        public decimal CombinedCashSales => CashSales + OpeningCashSales;
        public decimal CombinedSeniorDiscount => SeniorDiscount + OpeningSeniorDiscount;
        public decimal CombinedPwdDiscount => PwdDiscount + OpeningPwdDiscount;
        public decimal CombinedLoyaltyCardDiscount => LoyaltyCardDiscount + OpeningLoyaltyCardDiscount;
        public decimal CombinedGiftVoucherDiscount => GiftVoucherDiscount + OpeningGiftVoucherDiscount;
        public decimal CombinedEmployeeTenPercentDiscount => EmployeeTenPercentDiscount + OpeningEmployeeTenPercentDiscount;
        public decimal CombinedEmployeeFivePercentDiscount => EmployeeFivePercentDiscount + OpeningEmployeeFivePercentDiscount;
        public decimal CombinedEaglesDiscount => EaglesDiscount + OpeningEaglesDiscount;
        public decimal CombinedTotalDiscounts => TotalDiscounts + OpeningTotalDiscounts;
        public decimal CombinedSalesShortageAmount => SalesShortageAmount + OpeningSalesShortageAmount;
        public decimal CombinedSalesOverageAmount => SalesOverageAmount + OpeningSalesOverageAmount;
        public decimal CombinedRestoPcf => RestoPcf + OpeningRestoPcf;
        public decimal CombinedPcfFromSales => PcfFromSales + OpeningPcfFromSales;
        public decimal CombinedChangeAmount => ChangeAmount + OpeningChangeAmount;

        public decimal OpeningTotalDiscounts => OpeningSeniorDiscount + OpeningPwdDiscount + OpeningLoyaltyCardDiscount + OpeningGiftVoucherDiscount + OpeningEmployeeTenPercentDiscount + OpeningEmployeeFivePercentDiscount + OpeningEaglesDiscount;

        public decimal OpeningExpectedCashToHandover
        {
            get
            {
                decimal nonCash = OpeningTotalGCash + OpeningTotalCredit;
                decimal otherPayments = OpeningTotalBankTransfer + OpeningTotalCard + OpeningTotalRunawayCustomer;
                if (otherPayments == 0m)
                {
                    otherPayments = OpeningOtherPaymentAmount;
                }
                return OpeningGrossSales - nonCash - otherPayments;
            }
        }
        public decimal OpeningShortOverAmount => OpeningCashSales - OpeningExpectedCashToHandover;
        public string OpeningShortOverLabel => OpeningShortOverAmount < 0 ? "Short" : OpeningShortOverAmount > 0 ? "Over" : "Balanced";

        [StringLength(50)]
        public string? ReceiptNumberStart { get; set; }
        [StringLength(50)]
        public string? ReceiptNumberEnd { get; set; }
        [StringLength(100)]
        public string? WitnessName { get; set; }
        [StringLength(255)]
        public string? Notes { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public List<string>? ImageUrls { get; set; }
        public List<string>? ClosingImageUrls { get; set; }
        public SalesReportStatus Status { get; set; } = SalesReportStatus.Draft;
        public DocumentReviewStatus ReviewStatus { get; set; } = DocumentReviewStatus.Draft;
        public bool CanConfirmToTreasury { get; set; }
        public string StatusText => Status switch
        {
            SalesReportStatus.PendingManagerVerification => "Pending Manager Verification",
            SalesReportStatus.Confirmed => "Confirmed",
            SalesReportStatus.Rejected => "Rejected",
            SalesReportStatus.Adjusted => "Adjusted",
            SalesReportStatus.Uploaded => "Uploaded",
            SalesReportStatus.Parsed => "Parsed",
            _ => SalesReportId.HasValue ? "Reviewing Draft" : "New Intake"
        };
        public string PrimaryActionType => CanConfirmToTreasury ? "Confirm" : "SubmitForVerification";
        public string PrimaryActionText => CanConfirmToTreasury ? "Confirm to Treasury" : "Submit for Manager Verification";
        public string PrimaryActionIcon => CanConfirmToTreasury ? "check_circle" : "send";
        public List<CashBreakdownLineViewModel> OpeningItems { get; set; } = new();
        public List<CashBreakdownLineViewModel> Items { get; set; } = new();
    }

    public class CashBreakdownLineViewModel
    {
        public int Id { get; set; }
        public decimal Denomination { get; set; }
        public int Quantity { get; set; }
        public decimal Total { get; set; }
    }

    public class SalesReportLineViewModel
    {
        public int Id { get; set; }
        public SalesReportLineType LineType { get; set; }
        public decimal Amount { get; set; }
        [StringLength(100)]
        public string? Label { get; set; }
        public int SortOrder { get; set; }
    }

}
