# Antigravity Daily Sales Logbook Form Handoff

## Goal

Redesign the BranchStaff daily sales edit/review page so it matches the paper/logbook format used by branch staff for closing sales. The current page is a manager-style reconciliation form and only stores a few summary totals. The requested change needs a real data-model expansion plus a phone-friendly form.

## User context

The owner provided this sample logbook entry:

```text
August 13, 2026
Thursdays

CLOSING
B4 SALES
Cashier Name: tereh

Daily Gross Sales: ₱ 8,935.2
(Opening + Closing)

Closing Gross Sales: 5,773
Food Sales: ₱ 5,913
Beer Sales: ₱795
Beverages Sales: ₱ 100
Other Sales: ₱

Cash Sales: ₱ 4,788

G-Cash sales:
₱456

Bank Transfer:
₱ 529 BDO

Card:
Credit:
Run-away Customer:

SENIOR:
PWD:
LOYALTY CARD:
GIFT VOUCHER:
EMPLOYEE 10%:
EMPLOYEE 5%:
Eagles 5%:

Sales Shortage Amount/Reason:
Sales Overage Amount/Reason:

Resto PCF:
PCF from sales:
Change:

Expenses from Sales:
-
-
-
```

The owner wants BranchStaff to input the accumulated logbook data here. This is specifically the BranchStaff side of the daily sales workflow.

## Current implementation found

### Main files

- `AuditCkDayo/Controllers/SalesReportsController.cs`
  - `Upload()` GET/POST creates a `DocumentRecord` and `SalesReport`.
  - `Review(int id)` displays `SalesReportReviewViewModel`.
  - `Review(SalesReportReviewViewModel model, string actionType)` saves draft, submits for manager verification, or confirms to treasury.
  - `PostConfirmedSalesReportToTreasuryAsync()` posts `report.ConfirmedCashToHandover` as treasury cash-in.
  - `NotifyUploaderOfShortOverAsync()` calculates variance as:
    - `expected = GrossSales - GCashAmount - CreditAmount - OtherPaymentAmount`
    - `shortOver = ConfirmedCashToHandover - expected`

- `AuditCkDayo/Views/SalesReports/Upload.cshtml`
  - BranchStaff uploads 1-5 report images.
  - BranchStaff branch is fixed to assigned branch.
  - Captures only `businessDate`, `handoverDate`, `cashierName`, and images before OCR/review.

- `AuditCkDayo/Views/SalesReports/Review.cshtml`
  - Current form fields:
    - `EstablishmentId`
    - `BusinessDate`
    - `HandoverDate`
    - `CashierName`
    - `GrossSales`
    - `CashOut`
    - `ConfirmedCashToHandover`
    - `GCashAmount`
    - `CreditAmount`
    - `OtherPaymentAmount`
    - `ReceiptNumberStart`
    - `ReceiptNumberEnd`
    - `WitnessName`
    - `Notes`
    - cash denomination rows in `Items`
  - JavaScript recomputes cash denomination total and short/over notice.
  - This page is not shaped like the branch logbook.

- `AuditCkDayo/Models/SalesReport.cs`
  - Current stored fields are too limited for the logbook.

- `AuditCkDayo/Models/CashBreakdownLine.cs`
  - Stores denomination rows only.

- `AuditCkDayo/ViewModels/SalesReportReviewViewModel.cs`
  - Current view model mirrors limited stored fields.

- `AuditCkDayo.Tests/UnitTest1.cs`
  - Existing tests cover sales report upload/review/confirmation, cash breakdown persistence, treasury posting, role scoping, reports, and seeded QA data.

## Recommended design

Implement the exact logbook form, not a totals-only shortcut.

### BranchStaff Review page behavior

For BranchStaff, `/SalesReports/Review/{id}` should show a phone-first daily sales encoding form with these sections:

1. Header
   - Business date
   - Auto day label from date, e.g. Thursday
   - Branch
   - Report title/type, e.g. Closing / B4 Sales
   - Cashier name

2. Sales totals
   - Daily Gross Sales
   - Closing Gross Sales
   - Food Sales
   - Beer Sales
   - Beverages Sales
   - Other Sales

3. Payment collections
   - Cash Sales
   - GCash repeated amount lines
   - Bank Transfer repeated amount + label, e.g. `529 BDO`
   - Card repeated amount lines
   - Credit repeated amount + optional label/reason
   - Run-away Customer repeated amount + optional label/reason

4. Discounts
   - Senior
   - PWD
   - Loyalty Card
   - Gift Voucher
   - Employee 10%
   - Employee 5%
   - Eagles 5%

5. Variance / PCF / change
   - Sales Shortage Amount + Reason
   - Sales Overage Amount + Reason
   - Resto PCF
   - PCF from Sales
   - Change

6. Expenses from Sales
   - Repeatable description + amount lines

7. Computed summary
   - Total non-cash payments
   - Total discounts
   - Total expenses from sales
   - Expected cash to handover
   - Counted cash / cash to handover
   - Short/over warning

### Manager/Owner behavior

Keep Manager/Owner verification recognizable. They can see the submitted BranchStaff logbook data and still confirm to treasury.

Preserve current treasury posting rule unless the owner says otherwise:

- BranchStaff save/submit does not post to treasury.
- Manager/Owner confirmation posts `ConfirmedCashToHandover` as treasury cash-in.

## Data model changes

Add scalar fields to `SalesReport`:

```csharp
[Column(TypeName = "decimal(12,2)")]
public decimal ClosingGrossSales { get; set; }

[Column(TypeName = "decimal(12,2)")]
public decimal FoodSales { get; set; }

[Column(TypeName = "decimal(12,2)")]
public decimal BeerSales { get; set; }

[Column(TypeName = "decimal(12,2)")]
public decimal BeverageSales { get; set; }

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
```

Add a new child model for flexible line items:

```csharp
public enum SalesReportLineType
{
    GCash,
    BankTransfer,
    Card,
    Credit,
    RunawayCustomer,
    ExpenseFromSales
}

public class SalesReportLine
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int SalesReportId { get; set; }

    [ForeignKey("SalesReportId")]
    public virtual SalesReport SalesReport { get; set; } = null!;

    [Required]
    public SalesReportLineType LineType { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; }

    [MaxLength(100)]
    public string? Label { get; set; }

    public int SortOrder { get; set; }
}
```

Add to `SalesReport`:

```csharp
public virtual ICollection<SalesReportLine> Lines { get; set; } = new List<SalesReportLine>();
```

Add to `AuditDbContext`:

```csharp
public DbSet<SalesReportLine> SalesReportLines { get; set; }
```

Configure enum conversion and relationship in `OnModelCreating`:

```csharp
modelBuilder.Entity<SalesReportLine>()
    .Property(l => l.LineType)
    .HasConversion<string>()
    .HasMaxLength(50);

modelBuilder.Entity<SalesReportLine>()
    .HasOne(l => l.SalesReport)
    .WithMany(r => r.Lines)
    .HasForeignKey(l => l.SalesReportId)
    .OnDelete(DeleteBehavior.Cascade);
```

Create an EF migration after editing models/context:

```bash
dotnet ef migrations add SalesReportLogbookFields --project AuditCkDayo/AuditCkDayo.csproj --startup-project AuditCkDayo/AuditCkDayo.csproj
```

## ViewModel changes

Extend `SalesReportReviewViewModel` with the same scalar fields and line collections.

Suggested line view model:

```csharp
public class SalesReportLineViewModel
{
    public int Id { get; set; }
    public SalesReportLineType LineType { get; set; }
    public decimal Amount { get; set; }
    [StringLength(100)]
    public string? Label { get; set; }
    public int SortOrder { get; set; }
}
```

Suggested collection properties:

```csharp
public List<SalesReportLineViewModel> GCashLines { get; set; } = new();
public List<SalesReportLineViewModel> BankTransferLines { get; set; } = new();
public List<SalesReportLineViewModel> CardLines { get; set; } = new();
public List<SalesReportLineViewModel> CreditLines { get; set; } = new();
public List<SalesReportLineViewModel> RunawayCustomerLines { get; set; } = new();
public List<SalesReportLineViewModel> ExpenseFromSalesLines { get; set; } = new();
```

Computed properties:

```csharp
public decimal TotalGCash => GCashLines.Sum(l => l.Amount);
public decimal TotalBankTransfer => BankTransferLines.Sum(l => l.Amount);
public decimal TotalCard => CardLines.Sum(l => l.Amount);
public decimal TotalCredit => CreditLines.Sum(l => l.Amount);
public decimal TotalRunawayCustomer => RunawayCustomerLines.Sum(l => l.Amount);
public decimal TotalExpensesFromSales => ExpenseFromSalesLines.Sum(l => l.Amount);
public decimal TotalDiscounts => SeniorDiscount + PwdDiscount + LoyaltyCardDiscount + GiftVoucherDiscount + EmployeeTenPercentDiscount + EmployeeFivePercentDiscount + EaglesDiscount;
public decimal TotalNonCashPayments => TotalGCash + TotalBankTransfer + TotalCard + TotalCredit + OtherPaymentAmount;
```

Important compatibility mapping:

- Keep `GCashAmount`, `CreditAmount`, and `OtherPaymentAmount` populated from line totals for existing reports/dashboard logic.
- Recommended mapping:
  - `GCashAmount = TotalGCash`
  - `CreditAmount = TotalCredit`
  - `OtherPaymentAmount = TotalBankTransfer + TotalCard + TotalRunawayCustomer + OtherSales` only if business agrees; otherwise use explicit `OtherPaymentAmount` as before and include bank/card/runaway separately in expected-cash formula.

Open business decision for owner/product:

- Should `ExpectedCashToHandover` subtract bank transfer, card, and run-away customer separately? Current formula only subtracts GCash/Credit/OtherPayment. For the logbook form, expected cash should likely be:

```csharp
ExpectedCashToHandover = GrossSales
    - TotalGCash
    - TotalBankTransfer
    - TotalCard
    - TotalCredit
    - TotalRunawayCustomer;
```

Do not subtract PCF expenses from expected cash unless the owner explicitly changes that rule. A prior regression test asserts PCF expenses do not reduce expected cash.

## Controller changes

Update `SalesReportsController`:

1. Include `Lines` wherever review needs the report:

```csharp
.Include(r => r.Lines)
```

2. `ToReviewModel(report)` must map:
   - new scalar fields
   - `report.Lines` grouped by `LineType` and ordered by `SortOrder`

3. `ApplyReviewModel(report, model)` must:
   - copy scalar fields
   - calculate existing compatibility totals from logbook line collections
   - set `ConfirmedCashToHandover` from `CashSales` or from counted cash field, depending final UI decision

Recommended first version:

- Keep an explicit `ConfirmedCashToHandover` / `Cash to Handover` field in the summary.
- Do not silently set it equal to `CashSales`; cash counted and cash sales may differ when there is shortage/overage/change.

4. In POST `Review`, replace existing `report.Lines` with submitted non-empty lines:

```csharp
_context.SalesReportLines.RemoveRange(report.Lines);
report.Lines.Clear();
AddLines(report, model.GCashLines, SalesReportLineType.GCash);
AddLines(report, model.BankTransferLines, SalesReportLineType.BankTransfer);
AddLines(report, model.CardLines, SalesReportLineType.Card);
AddLines(report, model.CreditLines, SalesReportLineType.Credit);
AddLines(report, model.RunawayCustomerLines, SalesReportLineType.RunawayCustomer);
AddLines(report, model.ExpenseFromSalesLines, SalesReportLineType.ExpenseFromSales);
```

Only save rows where `Amount > 0` or `Label` is not blank.

5. Preserve current status transitions:
   - `SaveDraft` => Draft
   - `SubmitForVerification` => PendingManagerVerification
   - `Confirm` by Manager/Owner => Confirmed + treasury posting

## Razor redesign

File: `AuditCkDayo/Views/SalesReports/Review.cshtml`

Recommended approach:

- Keep uploaded image preview on top or collapsed for mobile.
- For BranchStaff, render the logbook form sections.
- For Manager/Owner, either:
  - render the same logbook fields read/write with confirm button, or
  - render a manager review layout using the same fields.

BranchStaff should not see a desktop table for primary input. Use cards and full-width fields.

Use existing Tailwind utility style already present in the file:

- `bg-surface-container-lowest`
- `border-surface-border`
- `rounded-xl`
- `font-label-caps`
- `font-data-mono`
- `text-on-surface`
- `text-on-surface-variant`
- `text-primary`

Suggested reusable partial-like Razor pattern inside same file first, to avoid over-abstraction:

```razor
<div class="rounded-xl border border-surface-border bg-surface-container-lowest p-space-4 space-y-space-4">
    <div class="flex items-center justify-between gap-space-3">
        <h2 class="font-label-caps text-on-surface uppercase tracking-wider text-[11px]">Payment Collections</h2>
        <span class="text-[11px] text-on-surface-variant">Add every line from the logbook.</span>
    </div>
    <!-- line groups here -->
</div>
```

Line groups need JavaScript add/remove/reindex behavior similar to current cash denomination rows.

## Tests to write first

Use TDD. Add failing tests before production changes.

File: `AuditCkDayo.Tests/UnitTest1.cs`

### Test 1: SalesReport persists logbook scalar fields and lines

Name:

```csharp
SalesReport_PersistsLogbookFieldsAndFlexibleLines
```

Expected behavior:

- Save a `SalesReport` with:
  - `ClosingGrossSales = 5773m`
  - `FoodSales = 5913m`
  - `BeerSales = 795m`
  - `BeverageSales = 100m`
  - `CashSales = 4788m`
  - `RestoPcf`, `PcfFromSales`, `ChangeAmount`
  - one GCash line: `456`
  - one BankTransfer line: `529`, label `BDO`
  - one ExpenseFromSales line
- Reload from EF including `Lines`.
- Assert scalar values and line types/labels/amounts persist.

Run red:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter SalesReport_PersistsLogbookFieldsAndFlexibleLines --artifacts-path .tmp-test-artifacts-sales-logbook-red
```

Expected before implementation: compile failure because fields/types do not exist.

### Test 2: Review submit saves BranchStaff logbook details

Name:

```csharp
Review_SubmitForVerification_SavesBranchStaffLogbookDetails
```

Expected behavior:

- Seed BranchStaff assigned to branch.
- Seed `DocumentRecord` and draft `SalesReport`.
- Build `SalesReportReviewViewModel` with sample values from owner.
- Call `SalesReportsController.Review(model, "SubmitForVerification")` as BranchStaff.
- Assert:
  - redirect to Review
  - report status is `PendingManagerVerification`
  - scalar fields persisted
  - GCash/BankTransfer/ExpenseFromSales lines persisted
  - compatibility totals persisted (`GCashAmount`, etc.)

Run red:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter Review_SubmitForVerification_SavesBranchStaffLogbookDetails --artifacts-path .tmp-test-artifacts-sales-logbook-workflow-red
```

Expected before implementation: compile failure or assertion failure because fields/lines are not implemented.

### Test 3: Manager confirmation still posts treasury cash-in

Name:

```csharp
Review_ConfirmWithLogbookDetails_PostsConfirmedCashToTreasury
```

Expected behavior:

- Draft or pending report with logbook detail lines.
- Manager confirms.
- `CashFlowEntry` is created/updated with:
  - `Direction = In`
  - `Category = Sales`
  - `Amount = model.ConfirmedCashToHandover`
  - `SourceDocumentId = report.DocumentRecordId`

This guards against breaking the existing treasury behavior.

## Verification commands

Run focused tests:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter "SalesReport_PersistsLogbookFieldsAndFlexibleLines|Review_SubmitForVerification_SavesBranchStaffLogbookDetails|Review_ConfirmWithLogbookDetails_PostsConfirmedCashToTreasury" --artifacts-path .tmp-test-artifacts-sales-logbook-focused
```

Run existing sales/report coverage:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter "SalesReport|SalesReports|ReportsAuditPacketTests|TreasuryReport" --artifacts-path .tmp-test-artifacts-sales-logbook-regression
```

Run full suite:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --artifacts-path .tmp-test-artifacts-sales-logbook-full
```

Browser smoke after build:

1. Start app from built DLL or `dotnet run` on a free port.
2. Login as BranchStaff, e.g. seeded `staff1@test.com` / `Password123!` if available in the current database.
3. Open `/SalesReports/Upload`.
4. Upload or use an existing draft report.
5. Open `/SalesReports/Review/{id}`.
6. Enter the sample values:
   - Cashier: `tereh`
   - Daily gross: `8935.20`
   - Closing gross: `5773`
   - Food: `5913`
   - Beer: `795`
   - Beverages: `100`
   - Cash: `4788`
   - GCash: `456`
   - Bank transfer: `529`, label `BDO`
7. Save draft, reload, verify values remain.
8. Submit for manager verification.
9. Login as Manager/Owner, confirm the report.
10. Open Treasury for handover date and verify cash-in entry exists.

## Risks / decisions to confirm

1. Expected cash formula must be confirmed.
   - Current formula: `GrossSales - GCash - Credit - OtherPayment`.
   - Logbook likely needs: `GrossSales - GCash - BankTransfer - Card - Credit - RunawayCustomer`.
   - PCF expenses should not be subtracted unless owner says so; prior tests protect that behavior.

2. Bank transfer/card/run-away storage should not be flattened into `OtherPaymentAmount` if future reports need separate categories.

3. Razor file may become large. Keep first implementation in `Review.cshtml` for speed, but if it becomes hard to maintain, split into partials later:
   - `_SalesReportImagePreview.cshtml`
   - `_SalesReportLogbookForm.cshtml`
   - `_SalesReportManagerSummary.cshtml`

4. Existing nullable warnings are already present. Do not treat them as new failures unless the change adds new warnings.

## Recommended implementation order

1. Add failing persistence test.
2. Add `SalesReportLine` model, enum, DbSet, relationship config, migration.
3. Make persistence test pass.
4. Add failing workflow test for BranchStaff submit.
5. Extend `SalesReportReviewViewModel`.
6. Update `SalesReportsController` mapping and POST save logic.
7. Make workflow test pass.
8. Redesign `Review.cshtml` for logbook sections.
9. Run focused tests, regression tests, full suite.
10. Browser-smoke BranchStaff save/submit and Manager confirm.

## Current status

No implementation was done in this handoff. The file is intentionally a detailed build plan for Antigravity because remaining assistant usage may not safely complete the full database + UI change in this session.
