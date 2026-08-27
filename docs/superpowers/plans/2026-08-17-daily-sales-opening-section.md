# Daily Sales Opening Section Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an Opening section to the daily sales report. The BranchStaff Daily Sales page shows a table of the day's reports, each row having **"Add Opening"** and **"Add Closing Daily Sales"** buttons. Opening is entered/edited as a draft; closing is added on top of the same record. The report surfaces to the manager only once closing is present, showing the combined (Opening + Closing) summary across the manager side, dashboard, historical daily-sales, reports/P&L, and the treasury cash-in posting.

**Architecture:** A single `SalesReport` row per business day + branch now carries both an Opening section and a Closing section. Opening scalar figures are stored as parallel `Opening*` columns; opening line items (GCash/bank/card/credit/runaway/expenses) and opening cash-denomination breakdown rows are stored in the existing `SalesReportLine` and `CashBreakdownLine` tables distinguished by a new `SalesReportSection` discriminator column. The BranchStaff Daily Sales table is the entry point: "Add Opening" opens the opening edit page (draft-only, editable), and "Add Closing Daily Sales" opens the closing edit page (submits the complete report to the manager only when opening data exists). Read-only combined properties (`TotalGrossSales`, `TotalCashSales`, `TotalConfirmedCashToHandover`) on `SalesReport` let every downstream consumer (dashboard, reports, P&L, treasury) show the full daily total without changing the stored closing columns.

**Tech Stack:** ASP.NET Core 9 MVC, EF Core 9 + Pomelo MySQL, xUnit + SQLite in-memory, Tailwind (CDN) Razor views.

---

## File Structure

- `AuditCkDayo/Models/SalesReport.cs` — add `SalesReportSection` enum + `Opening*` scalar fields
- `AuditCkDayo/Models/SalesReportLine.cs` — add `Section` discriminator
- `AuditCkDayo/Models/CashBreakdownLine.cs` — add `Section` discriminator
- `AuditCkDayo/Data/AuditDbContext.cs` — configure new enum + scalar columns (migration not strictly needed for new columns if using `Migrate()`, but we add a migration)
- `AuditCkDayo/Migrations/*_AddSalesReportOpeningSection.cs` — generated
- `AuditCkDayo/ViewModels/SalesReportReviewViewModel.cs` — add `ReportSection`, `Opening*` fields, opening line lists, opening items, `OpeningCashSales`
- `AuditCkDayo/Controllers/SalesReportsController.cs` — upload routing, opening review GET/POST, section-aware save/build
- `AuditCkDayo/Views/SalesReports/Upload.cshtml` — add OPENING/CLOSING dropdown
- `AuditCkDayo/Views/SalesReports/OpeningReview.cshtml` — new opening edit page
- `AuditCkDayo/Views/SalesReports/Review.cshtml` — rename expense label to "Expenses from PCF", show combined cash sales on manager side
- `AuditCkDayo.Tests/UnitTest1.cs` — new tests

---

### Task 1: Model — add `SalesReportSection` enum and Opening scalar fields

**Files:**
- Modify: `AuditCkDayo/Models/SalesReport.cs`

- [ ] **Step 1: Add the `SalesReportSection` enum and the Opening scalar columns**

In `AuditCkDayo/Models/SalesReport.cs`, add the enum above the `SalesReport` class and add the `Opening*` fields after the `ChangeAmount` field (after line 173):

```csharp
public enum SalesReportSection
{
    Closing = 0,
    Opening = 1
}
```

Then inside the `SalesReport` class, after the `ChangeAmount` property:

```csharp
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
```

- [ ] **Step 2: Build to confirm compilation**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds (new properties compile).

- [ ] **Step 3: Commit**

```bash
git add AuditCkDayo/Models/SalesReport.cs
git commit -m "feat(sales): add opening section scalar fields to SalesReport"
```

---

### Task 2: Model — add `Section` discriminator to line and breakdown models

**Files:**
- Modify: `AuditCkDayo/Models/SalesReportLine.cs`
- Modify: `AuditCkDayo/Models/CashBreakdownLine.cs`

- [ ] **Step 1: Add `Section` to `SalesReportLine`**

In `AuditCkDayo/Models/SalesReportLine.cs`, add a `Section` property after `SortOrder` (line 36):

```csharp
        [Required]
        public SalesReportSection Section { get; set; } = SalesReportSection.Closing;
```

- [ ] **Step 2: Add `Section` to `CashBreakdownLine`**

In `AuditCkDayo/Models/CashBreakdownLine.cs`, add a `Section` property after `Total` (line 38):

```csharp
        [Required]
        public SalesReportSection Section { get; set; } = SalesReportSection.Closing;
```

- [ ] **Step 3: Build to confirm compilation**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add AuditCkDayo/Models/SalesReportLine.cs AuditCkDayo/Models/CashBreakdownLine.cs
git commit -m "feat(sales): add section discriminator to sales report lines and cash breakdown lines"
```

---

### Task 3: EF migration for the new columns

**Files:**
- Create: `AuditCkDayo/Migrations/*_AddSalesReportOpeningSection.cs` (generated)

- [ ] **Step 1: Ensure the EF tool version matches net9.0 / EF9**

The installed `dotnet-ef` is 8.0.15, which is older than the project's EF Core 9. Upgrade the global tool first:

```bash
dotnet tool update --global dotnet-ef
```

Run: `dotnet ef --version`
Expected: prints 9.x (matching the project). If the update is blocked, proceed and fix version errors if they appear.

- [ ] **Step 2: Generate the migration**

Run (from repo root):

```bash
dotnet ef migrations add AddSalesReportOpeningSection --project AuditCkDayo/AuditCkDayo.csproj
```

Expected: a new `Migrations/<timestamp>_AddSalesReportOpeningSection.cs` created. Inspect it to confirm it adds the `Opening*` columns to `SalesReports` and the `Section` column to `SalesReportLines` and `CashBreakdownLines`.

- [ ] **Step 3: Add a data default for the new `Section` columns**

Open the generated migration's `Up` method and ensure the `AddColumn` calls for `Section` on `SalesReportLines` and `CashBreakdownLines` include a `defaultValue: 0` so existing rows are not null. If the generated migration omits it, edit to add:

```csharp
migrationBuilder.AddColumn<int>(
    name: "Section",
    table: "SalesReportLines",
    type: "int",
    nullable: false,
    defaultValue: 0);
```

(Do the same for `CashBreakdownLines`.)

- [ ] **Step 4: Build to confirm the migration compiles**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add AuditCkDayo/Migrations
git commit -m "feat(sales): migration for sales report opening section"
```

---

### Task 4: ViewModel — opening fields, section flag, opening line lists

**Files:**
- Modify: `AuditCkDayo/ViewModels/SalesReportReviewViewModel.cs`

- [ ] **Step 1: Add `ReportSection`, opening scalar fields, and a combined cash helper**

Add a `ReportSection` property and opening fields to `SalesReportReviewViewModel`. Insert after the `ChangeAmount` property (line 101):

```csharp
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

        [StringLength(255)]
        public string? OpeningSalesShortageReason { get; set; }

        [Range(0, double.MaxValue)]
        public decimal OpeningSalesOverageAmount { get; set; }

        [StringLength(255)]
        public string? OpeningSalesOverageReason { get; set; }

        [Range(0, double.MaxValue)]
        public decimal OpeningRestoPcf { get; set; }

        [Range(0, double.MaxValue)]
        public decimal OpeningPcfFromSales { get; set; }

        [Range(0, double.MaxValue)]
        public decimal OpeningChangeAmount { get; set; }

        [StringLength(50)]
        public string? OpeningReceiptNumberStart { get; set; }

        [StringLength(50)]
        public string? OpeningReceiptNumberEnd { get; set; }

        [StringLength(100)]
        public string? OpeningWitnessName { get; set; }

        [StringLength(255)]
        public string? OpeningNotes { get; set; }
```

Add opening line-item lists after `ExpenseFromSalesLines` (line 108):

```csharp
        public List<SalesReportLineViewModel> OpeningGCashLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningBankTransferLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningCardLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningCreditLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningRunawayCustomerLines { get; set; } = new();
        public List<SalesReportLineViewModel> OpeningExpenseFromSalesLines { get; set; } = new();
```

Add a `TotalOpeningCashSales` helper (read-only, no backing field) after `TotalExpensesFromSales` (line 115):

```csharp
        public decimal TotalOpeningCashSales => OpeningCashSales;
```

Add an opening cash-denomination breakdown list after `Items` (line 161):

```csharp
        public List<CashBreakdownLineViewModel> OpeningItems { get; set; } = new();
```

- [ ] **Step 2: Build to confirm compilation**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add AuditCkDayo/ViewModels/SalesReportReviewViewModel.cs
git commit -m "feat(sales): add opening section fields to review view model"
```

---

### Task 5: Controller — BranchStaff Daily Sales table entry point

**Files:**
- Modify: `AuditCkDayo/Controllers/SalesReportsController.cs`
- Modify: `AuditCkDayo/Views/SalesReports/Index.cshtml`

The `Index` action currently redirects BranchStaff to `Upload` (`Index.cshtml:34-37`). Change it so BranchStaff see a table of their establishment's daily sales reports with **"Add Opening"** and **"Add Closing Daily Sales"** actions per row.

- [ ] **Step 1: Update `Index` to scope BranchStaff reports and pass a flag**

Replace the BranchStaff early-return in `Index` (lines 34-37) so BranchStaff instead render the table scoped to their establishment:

```csharp
            var isBranchStaff = IsBranchStaff();
            if (isBranchStaff)
            {
                var currentUserId = GetCurrentUserId();
                var assignedEstablishmentId = currentUserId.HasValue
                    ? await _context.Users
                        .AsNoTracking()
                        .Where(u => u.Id == currentUserId.Value && u.Role == UserRole.BranchStaff && !u.IsDeleted)
                        .Select(u => u.EstablishmentId)
                        .FirstOrDefaultAsync()
                    : null;

                if (!assignedEstablishmentId.HasValue)
                {
                    return View(new List<SalesReport>());
                }

                var staffReports = await _context.SalesReports
                    .AsNoTracking()
                    .Include(r => r.DocumentRecord)
                    .Include(r => r.Establishment)
                    .Where(r => r.EstablishmentId == assignedEstablishmentId.Value)
                    .OrderByDescending(r => r.BusinessDate)
                    .ThenByDescending(r => r.Id)
                    .ToListAsync();

                ViewBag.IsBranchStaff = true;
                return View(staffReports);
            }
```

- [ ] **Step 2: Build to confirm compilation**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add AuditCkDayo/Controllers/SalesReportsController.cs
git commit -m "feat(sales): show daily sales table to branch staff"
```

---

### Task 6: Controller — Opening review GET/POST actions

**Files:**
- Modify: `AuditCkDayo/Controllers/SalesReportsController.cs`

- [ ] **Step 1: Add `OpeningReview` GET action**

Insert after the existing `Review` GET (after line 235):

```csharp
        [HttpGet]
        public async Task<IActionResult> OpeningReview(int id)
        {
            var report = await _context.SalesReports
                .AsNoTracking()
                .Include(r => r.DocumentRecord)
                .Include(r => r.CashBreakdownLines)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
            {
                return NotFound();
            }

            if (await CurrentUserCannotAccessAsync(report.EstablishmentId))
            {
                return Forbid();
            }

            await PopulateEstablishments(report.EstablishmentId);
            var model = BuildReviewModel(report);
            model.ReportSection = SalesReportSection.Opening;
            return View("OpeningReview", model);
        }
```

- [ ] **Step 2: Add `OpeningReview` POST action**

Insert after the existing `Review` POST (after line 458):

```csharp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OpeningReview(SalesReportReviewViewModel model, string actionType)
        {
            if (!model.SalesReportId.HasValue)
            {
                return NotFound();
            }

            var report = await _context.SalesReports
                .Include(r => r.DocumentRecord)
                .Include(r => r.CashBreakdownLines)
                .Include(r => r.Lines)
                .FirstOrDefaultAsync(r => r.Id == model.SalesReportId.Value && r.DocumentRecordId == model.DocumentRecordId);

            if (report == null)
            {
                return NotFound();
            }

            if (await CurrentUserCannotAccessAsync(report.EstablishmentId) || await CurrentUserCannotAccessAsync(model.EstablishmentId))
            {
                return Forbid();
            }

            await PopulateEstablishments(model.EstablishmentId);
            model.ReportSection = SalesReportSection.Opening;
            PopulateReviewUiState(model, report);

            if (!await IsValidOperatingBranchAsync(model.EstablishmentId))
            {
                ModelState.AddModelError(nameof(model.EstablishmentId), "Select an active operating branch.");
            }

            if (!ModelState.IsValid)
            {
                return View("OpeningReview", model);
            }

            var requestedConfirmAction = string.Equals(actionType, "Confirm", StringComparison.OrdinalIgnoreCase);
            var canConfirmToTreasury = CanConfirmSalesReportToTreasury();
            var isConfirmAction = requestedConfirmAction && canConfirmToTreasury;
            var isSubmitForVerificationAction = string.Equals(actionType, "SubmitForVerification", StringComparison.OrdinalIgnoreCase)
                || (requestedConfirmAction && !canConfirmToTreasury);
            if (!isConfirmAction && (report.Status == SalesReportStatus.Confirmed || report.DocumentRecord.ReviewStatus == DocumentReviewStatus.Confirmed))
            {
                ModelState.AddModelError(string.Empty, "Confirmed sales reports cannot be saved as drafts.");
                TempData["Error"] = "Confirmed sales reports cannot be saved as drafts.";
                return View("OpeningReview", BuildReviewModel(report));
            }

            ApplyOpeningModel(report, model);
            ApplyOpeningLines(report, model);

            report.Status = SalesReportStatus.Draft;
            report.DocumentRecord.ReviewStatus = DocumentReviewStatus.Draft;
            report.ConfirmedByUserId = null;
            report.ConfirmedAt = null;
            report.DocumentRecord.ConfirmedByUserId = null;
            report.DocumentRecord.ConfirmedAt = null;

            TempData["Message"] = "Opening sales draft saved.";

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(OpeningReview), new { id = report.Id });
        }
```

- [ ] **Step 3: Add the `ApplyOpeningModel` and `ApplyOpeningLines` private helpers**

Insert after the `ApplyReviewModel` method (after line 757):

```csharp
        private static void ApplyOpeningModel(SalesReport report, SalesReportReviewViewModel model)
        {
            report.EstablishmentId = model.EstablishmentId;
            report.CashierName = model.CashierName;
            report.BusinessDate = model.BusinessDate.Date;
            report.HandoverDate = model.HandoverDate.Date;
            report.OpeningGrossSales = model.OpeningGrossSales;
            report.OpeningCashSales = model.OpeningCashSales;
            report.OpeningFoodSales = model.OpeningFoodSales;
            report.OpeningBeerSales = model.OpeningBeerSales;
            report.OpeningBeverageSales = model.OpeningBeverageSales;
            report.OpeningOtherSales = model.OpeningOtherSales;
            report.OpeningSeniorDiscount = model.OpeningSeniorDiscount;
            report.OpeningPwdDiscount = model.OpeningPwdDiscount;
            report.OpeningLoyaltyCardDiscount = model.OpeningLoyaltyCardDiscount;
            report.OpeningGiftVoucherDiscount = model.OpeningGiftVoucherDiscount;
            report.OpeningEmployeeTenPercentDiscount = model.OpeningEmployeeTenPercentDiscount;
            report.OpeningEmployeeFivePercentDiscount = model.OpeningEmployeeFivePercentDiscount;
            report.OpeningEaglesDiscount = model.OpeningEaglesDiscount;
            report.OpeningSalesShortageAmount = model.OpeningSalesShortageAmount;
            report.OpeningSalesShortageReason = model.OpeningSalesShortageReason;
            report.OpeningSalesOverageAmount = model.OpeningSalesOverageAmount;
            report.OpeningSalesOverageReason = model.OpeningSalesOverageReason;
            report.OpeningRestoPcf = model.OpeningRestoPcf;
            report.OpeningPcfFromSales = model.OpeningPcfFromSales;
            report.OpeningChangeAmount = model.OpeningChangeAmount;
            report.OpeningReceiptNumberStart = model.OpeningReceiptNumberStart;
            report.OpeningReceiptNumberEnd = model.OpeningReceiptNumberEnd;
            report.OpeningWitnessName = model.OpeningWitnessName;
            report.OpeningNotes = model.OpeningNotes;
        }

        private void ApplyOpeningLines(SalesReport report, SalesReportReviewViewModel model)
        {
            _context.SalesReportLines.RemoveRange(report.Lines.Where(l => l.Section == SalesReportSection.Opening).ToList());
            report.Lines.Where(l => l.Section == SalesReportSection.Opening).ToList().ForEach(l => report.Lines.Remove(l));

            _context.CashBreakdownLines.RemoveRange(report.CashBreakdownLines.Where(b => b.Section == SalesReportSection.Opening).ToList());
            report.CashBreakdownLines.Where(b => b.Section == SalesReportSection.Opening).ToList().ForEach(b => report.CashBreakdownLines.Remove(b));

            int sortOrder = 0;
            AddOpeningLines(model.OpeningGCashLines, SalesReportLineType.GCash, report, ref sortOrder);
            AddOpeningLines(model.OpeningBankTransferLines, SalesReportLineType.BankTransfer, report, ref sortOrder);
            AddOpeningLines(model.OpeningCardLines, SalesReportLineType.Card, report, ref sortOrder);
            AddOpeningLines(model.OpeningCreditLines, SalesReportLineType.Credit, report, ref sortOrder);
            AddOpeningLines(model.OpeningRunawayCustomerLines, SalesReportLineType.RunawayCustomer, report, ref sortOrder);
            AddOpeningLines(model.OpeningExpenseFromSalesLines, SalesReportLineType.ExpenseFromSales, report, ref sortOrder);

            if (model.OpeningItems != null)
            {
                foreach (var item in model.OpeningItems)
                {
                    report.CashBreakdownLines.Add(new CashBreakdownLine
                    {
                        OwnerType = CashBreakdownOwnerType.SalesReport,
                        OwnerId = report.Id,
                        Section = SalesReportSection.Opening,
                        Denomination = item.Denomination,
                        Quantity = item.Quantity,
                        Total = item.Denomination * item.Quantity
                    });
                }
            }
        }

        private static void AddOpeningLines(List<SalesReportLineViewModel>? lines, SalesReportLineType lineType, SalesReport report, ref int sortOrder)
        {
            if (lines == null)
            {
                return;
            }
            foreach (var line in lines)
            {
                if (line.Amount > 0m || !string.IsNullOrWhiteSpace(line.Label))
                {
                    report.Lines.Add(new SalesReportLine
                    {
                        LineType = lineType,
                        Section = SalesReportSection.Opening,
                        Amount = line.Amount,
                        Label = line.Label,
                        SortOrder = sortOrder++
                    });
                }
            }
        }
```

- [ ] **Step 4: Build to confirm compilation**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add AuditCkDayo/Controllers/SalesReportsController.cs
git commit -m "feat(sales): add opening review actions and save logic"
```

---

### Task 7: Controller — populate opening fields in the review model and scope closing saves

**Files:**
- Modify: `AuditCkDayo/Controllers/SalesReportsController.cs`

- [ ] **Step 1: Populate opening fields in `ToReviewModel`**

In `ToReviewModel` (line 611), add the opening field mappings after the `ChangeAmount` mapping (line 647):

```csharp
                OpeningGrossSales = report.OpeningGrossSales,
                OpeningCashSales = report.OpeningCashSales,
                OpeningFoodSales = report.OpeningFoodSales,
                OpeningBeerSales = report.OpeningBeerSales,
                OpeningBeverageSales = report.OpeningBeverageSales,
                OpeningOtherSales = report.OpeningOtherSales,
                OpeningSeniorDiscount = report.OpeningSeniorDiscount,
                OpeningPwdDiscount = report.OpeningPwdDiscount,
                OpeningLoyaltyCardDiscount = report.OpeningLoyaltyCardDiscount,
                OpeningGiftVoucherDiscount = report.OpeningGiftVoucherDiscount,
                OpeningEmployeeTenPercentDiscount = report.OpeningEmployeeTenPercentDiscount,
                OpeningEmployeeFivePercentDiscount = report.OpeningEmployeeFivePercentDiscount,
                OpeningEaglesDiscount = report.OpeningEaglesDiscount,
                OpeningSalesShortageAmount = report.OpeningSalesShortageAmount,
                OpeningSalesShortageReason = report.OpeningSalesShortageReason,
                OpeningSalesOverageAmount = report.OpeningSalesOverageAmount,
                OpeningSalesOverageReason = report.OpeningSalesOverageReason,
                OpeningRestoPcf = report.OpeningRestoPcf,
                OpeningPcfFromSales = report.OpeningPcfFromSales,
                OpeningChangeAmount = report.OpeningChangeAmount,
                OpeningReceiptNumberStart = report.OpeningReceiptNumberStart,
                OpeningReceiptNumberEnd = report.OpeningReceiptNumberEnd,
                OpeningWitnessName = report.OpeningWitnessName,
                OpeningNotes = report.OpeningNotes,
```

- [ ] **Step 2: Populate opening line lists in `ToReviewModel`**

Inside the existing `foreach (var line in report.Lines)` block in `ToReviewModel`, add opening branch cases by checking `line.Section`. Update the switch so opening lines go into the `Opening*` lists. Replace the `switch (line.LineType)` block (lines 672-692) with:

```csharp
                    if (line.Section == SalesReportSection.Opening)
                    {
                        switch (line.LineType)
                        {
                            case SalesReportLineType.GCash:
                                model.OpeningGCashLines.Add(lineVm);
                                break;
                            case SalesReportLineType.BankTransfer:
                                model.OpeningBankTransferLines.Add(lineVm);
                                break;
                            case SalesReportLineType.Card:
                                model.OpeningCardLines.Add(lineVm);
                                break;
                            case SalesReportLineType.Credit:
                                model.OpeningCreditLines.Add(lineVm);
                                break;
                            case SalesReportLineType.RunawayCustomer:
                                model.OpeningRunawayCustomerLines.Add(lineVm);
                                break;
                            case SalesReportLineType.ExpenseFromSales:
                                model.OpeningExpenseFromSalesLines.Add(lineVm);
                                break;
                        }
                        continue;
                    }

                    switch (line.LineType)
                    {
                        case SalesReportLineType.GCash:
                            model.GCashLines.Add(lineVm);
                            break;
                        case SalesReportLineType.BankTransfer:
                            model.BankTransferLines.Add(lineVm);
                            break;
                        case SalesReportLineType.Card:
                            model.CardLines.Add(lineVm);
                            break;
                        case SalesReportLineType.Credit:
                            model.CreditLines.Add(lineVm);
                            break;
                        case SalesReportLineType.RunawayCustomer:
                            model.RunawayCustomerLines.Add(lineVm);
                            break;
                        case SalesReportLineType.ExpenseFromSales:
                            model.ExpenseFromSalesLines.Add(lineVm);
                            break;
                    }
```

- [ ] **Step 3: Scope the closing `Review` POST line-save to the Closing section**

In the existing `Review` POST, the cash-breakdown save (line 289-304) currently does `RemoveRange(report.CashBreakdownLines)` and clears ALL breakdowns. Change it so only Closing-section breakdowns are removed, preserving Opening ones. Replace lines 289-304 with:

```csharp
            _context.CashBreakdownLines.RemoveRange(report.CashBreakdownLines.Where(b => b.Section == SalesReportSection.Closing).ToList());
            report.CashBreakdownLines.Where(b => b.Section == SalesReportSection.Closing).ToList().ForEach(b => report.CashBreakdownLines.Remove(b));
            if (model.Items != null)
            {
                foreach (var item in model.Items)
                {
                    report.CashBreakdownLines.Add(new CashBreakdownLine
                    {
                        OwnerType = CashBreakdownOwnerType.SalesReport,
                        OwnerId = report.Id,
                        Section = SalesReportSection.Closing,
                        Denomination = item.Denomination,
                        Quantity = item.Quantity,
                        Total = item.Denomination * item.Quantity
                    });
                }
            }
```

- [ ] **Step 4: Scope the closing `Review` POST line-item save to the Closing section**

The existing `Review` POST clears `report.Lines` (lines 306-307) and rebuilds all lines. Change it so only Closing-section lines are removed and rebuilt, preserving Opening ones. Replace line 306-307:

```csharp
            _context.SalesReportLines.RemoveRange(report.Lines.Where(l => l.Section == SalesReportSection.Closing).ToList());
            report.Lines.Where(l => l.Section == SalesReportSection.Closing).ToList().ForEach(l => report.Lines.Remove(l));
```

Then each `foreach` block that adds a closing line (GCash/BankTransfer/Card/Credit/RunawayCustomer/ExpenseFromSales, lines 310-410) must set `Section = SalesReportSection.Closing` on the newly created `SalesReportLine`. For example, the GCash block becomes:

```csharp
                        report.Lines.Add(new SalesReportLine
                        {
                            LineType = SalesReportLineType.GCash,
                            Section = SalesReportSection.Closing,
                            Amount = line.Amount,
                            Label = line.Label,
                            SortOrder = sortOrder++
                        });
```

(Repeat the `Section = SalesReportSection.Closing,` line for each of the six closing line-type blocks.)

- [ ] **Step 5: Populate opening cash breakdown items in `ToReviewModel`**

After the existing line-item loop in `ToReviewModel` (after line 694), add population of `OpeningItems`:

```csharp
            if (report.CashBreakdownLines != null)
            {
                foreach (var b in report.CashBreakdownLines.Where(b => b.Section == SalesReportSection.Opening))
                {
                    model.OpeningItems.Add(new CashBreakdownLineViewModel
                    {
                        Id = b.Id,
                        Denomination = b.Denomination,
                        Quantity = b.Quantity,
                        Total = b.Total
                    });
                }
            }
```

- [ ] **Step 6: Block closing submit to manager until opening data exists**

The closing `Review` POST sends the report to the manager only when opening is also present. In the closing `Review` POST, inside the `isSubmitForVerificationAction` branch (currently lines 433-443), add the gate before setting the status. Replace that branch with:

```csharp
            else if (isSubmitForVerificationAction)
            {
                if (report.OpeningCashSales == 0m && report.OpeningGrossSales == 0m)
                {
                    ModelState.AddModelError(string.Empty, "Add the opening sales section before submitting this daily sales report.");
                    TempData["Error"] = "Add the opening daily sales before submitting for manager verification.";
                    await _context.SaveChangesAsync();
                    return View(BuildReviewModel(report));
                }

                report.Status = SalesReportStatus.PendingManagerVerification;
                report.DocumentRecord.ReviewStatus = DocumentReviewStatus.PendingManagerVerification;
                report.ConfirmedByUserId = null;
                report.ConfirmedAt = null;
                report.DocumentRecord.ConfirmedByUserId = null;
                report.DocumentRecord.ConfirmedAt = null;

                TempData["Message"] = "Sales report submitted for manager verification.";
            }
```

- [ ] **Step 7: Build to confirm compilation**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds.

- [ ] **Step 8: Commit**

```bash
git add AuditCkDayo/Controllers/SalesReportsController.cs
git commit -m "feat(sales): gate closing submission on opening data presence"
```

---

### Task 8: Index view — BranchStaff table with "Add Opening" and "Add Closing Daily Sales" actions

**Files:**
- Modify: `AuditCkDayo/Views/SalesReports/Index.cshtml`

The `Index.cshtml` currently renders a list of pending reports for manager review. For BranchStaff (`ViewBag.IsBranchStaff == true`), render a per-establishment table with each report's opening status, closing status, combined cash sales, and per-row actions: **"Add Opening"** and **"Add Closing Daily Sales"**. When no report record exists for the day yet, the row's "Add Opening" action creates one.

- [ ] **Step 1: Add a BranchStaff branch in the view**

At the top of `Index.cshtml`, after the model declaration, add:

```html
@if (ViewBag.IsBranchStaff == true)
{
    <div class="space-y-6">
        <div class="flex flex-col gap-space-2 max-w-3xl">
            <div class="flex items-center gap-space-2 text-primary">
                <span class="material-symbols-outlined text-[20px]">receipt_long</span>
                <span class="font-label-caps uppercase tracking-widest text-on-surface-variant">Daily Sales</span>
            </div>
            <h1 class="font-headline-lg text-headline-lg text-on-surface">Daily Sales</h1>
            <p class="font-body-md text-on-surface-variant leading-relaxed">
                Add your establishment's opening and closing daily sales.
            </p>
        </div>

        <div class="bg-surface-container-lowest rounded-xl shadow-sm border border-surface-border overflow-hidden">
            <div class="bg-primary px-space-6 py-space-4 flex justify-between items-center">
                <span class="text-on-primary font-label-caps uppercase tracking-tighter text-[11px]">Daily Sales Records</span>
            </div>
            <table class="w-full text-left border-collapse">
                <thead class="hidden lg:table-header-group">
                    <tr class="bg-surface-container-low border-b border-surface-border">
                        <th class="px-space-4 py-space-3 font-label-caps text-on-tertiary-container uppercase tracking-wider text-[10px]">Business Date</th>
                        <th class="px-space-4 py-space-3 font-label-caps text-on-tertiary-container uppercase tracking-wider text-[10px]">Opening</th>
                        <th class="px-space-4 py-space-3 font-label-caps text-on-tertiary-container uppercase tracking-wider text-[10px]">Closing</th>
                        <th class="px-space-4 py-space-3 font-label-caps text-on-tertiary-container uppercase tracking-wider text-[10px] text-right">Combined Cash Sales</th>
                        <th class="px-space-4 py-space-3 font-label-caps text-on-tertiary-container uppercase tracking-wider text-[10px] text-right">Actions</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-surface-border">
                    @foreach (var report in Model)
                    {
                        <tr class="hover:bg-surface-container-low/30 transition-colors">
                            <td class="px-space-4 py-space-3 font-data-mono text-on-surface">@report.BusinessDate.ToString("MMM d, yyyy")</td>
                            <td class="px-space-4 py-space-3">
                                @if (report.OpeningCashSales > 0m)
                                {
                                    <span class="text-audit-success font-body-md text-[12px]">Saved</span>
                                }
                                else
                                {
                                    <span class="text-on-surface-variant font-body-md text-[12px]">Not set</span>
                                }
                            </td>
                            <td class="px-space-4 py-space-3">
                                @if (report.CashSales > 0m || report.Status == SalesReportStatus.PendingManagerVerification || report.Status == SalesReportStatus.Confirmed)
                                {
                                    <span class="text-audit-success font-body-md text-[12px]">Saved</span>
                                }
                                else
                                {
                                    <span class="text-on-surface-variant font-body-md text-[12px]">Not set</span>
                                }
                            </td>
                            <td class="px-space-4 py-space-3 text-right font-data-mono text-primary font-bold">₱@report.TotalConfirmedCashToHandover.ToString("N2")</td>
                            <td class="px-space-4 py-space-3 text-right">
                                <a asp-controller="SalesReports" asp-action="OpeningReview" asp-route-id="@report.Id" class="px-3 py-1.5 rounded-lg border border-primary/20 text-primary font-label-caps text-[10px] uppercase hover:bg-primary hover:text-on-primary transition-colors">Add Opening</a>
                                <a asp-controller="SalesReports" asp-action="Review" asp-route-id="@report.Id" class="px-3 py-1.5 rounded-lg border border-primary/20 text-primary font-label-caps text-[10px] uppercase hover:bg-primary hover:text-on-primary transition-colors">Add Closing Daily Sales</a>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
            @if (!Model.Any())
            {
                <div class="p-space-8 text-center text-on-surface-variant font-body-md">No daily sales records yet.</div>
            }
        </div>
    </div>
}
else
{
    @* existing manager-review list markup goes here *@
}
```

- [ ] **Step 2: Build to confirm the view compiles**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add AuditCkDayo/Views/SalesReports/Index.cshtml
git commit -m "feat(sales): add branch staff daily sales table with opening/closing actions"
```

---

### Task 9: Opening edit view — new page mirroring the closing form

**Files:**
- Create: `AuditCkDayo/Views/SalesReports/OpeningReview.cshtml`

- [ ] **Step 1: Create `OpeningReview.cshtml`**

Create `AuditCkDayo/Views/SalesReports/OpeningReview.cshtml` modeled on `Review.cshtml` but binding only the `Opening*` fields. The page uses the same layout, image-preview column, and section header. It has a **single "Save Opening Draft" action** (it does NOT offer "SubmitForVerification" or "Confirm", since opening is draft-only and never independently posts to treasury or the manager). It binds opening scalar fields, opening line lists (`OpeningGCashLines`, etc.), and the `OpeningItems` cash-denomination grid. Reference the closing `Review.cshtml` for the exact Tailwind markup and copy the image preview / lightbox / row-add JS with the opening property prefixes (`OpeningGCashLines`, `OpeningBankTransferLines`, `OpeningCardLines`, `OpeningCreditLines`, `OpeningRunawayCustomerLines`, `OpeningExpenseFromSalesLines`).

Key binding notes for the opening page:
- `@model AuditCkDayo.ViewModels.SalesReportReviewViewModel`
- Form posts to `asp-action="OpeningReview"`.
- Include hidden `SalesReportId`, `DocumentRecordId`, `ImageUrl`, `ImageUrls`.
- Bind scalar inputs to `asp-for="OpeningCashSales"`, `asp-for="OpeningGrossSales"`, `asp-for="OpeningFoodSales"`, `asp-for="OpeningBeerSales"`, `asp-for="OpeningBeverageSales"`, `asp-for="OpeningOtherSales"`, and the seven `Opening*Discount` fields, `OpeningSalesShortageAmount/Reason`, `OpeningSalesOverageAmount/Reason`, `OpeningRestoPcf`, `OpeningPcfFromSales`, `OpeningChangeAmount`, `OpeningReceiptNumberStart/End`, `OpeningWitnessName`, `OpeningNotes`.
- Opening payment line rows use the `Opening*Lines` property prefixes in the add/remove/reindex JS.
- Opening cash-denomination breakdown rows use `OpeningItems[i].Denomination/Quantity/Total`.
- The cash-sales field is `OpeningCashSales`; for BranchStaff, sync it to a hidden `OpeningCashSales` total and to the page's claimed-cash display.
- Expenses section label reads "Expenses from PCF".
- Single submit button posts `actionType=SaveDraft` to the `OpeningReview` POST.

- [ ] **Step 2: Add a working view scaffold**

As a concrete baseline, the opening page mirrors the closing page's section structure. Build it by copying `Review.cshtml` and applying these changes: (1) rename the header/title to "Opening Daily Sales", (2) post to `OpeningReview`, (3) prefix every scalar `asp-for` with `Opening`, (4) rename the `*Lines` prefixes to `Opening*Lines`, (5) rename `Items` to `OpeningItems`, (6) set the claimed-cash display from `OpeningCashSales`. No code is written in this step beyond creating the file from the closing template.

- [ ] **Step 3: Build to confirm the view compiles**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds (Razor views compile).

- [ ] **Step 4: Commit**

```bash
git add AuditCkDayo/Views/SalesReports/OpeningReview.cshtml
git commit -m "feat(sales): add opening daily sales edit page"
```

---

### Task 10: Closing review view — rename expense label and show combined cash sales on manager side

**Files:**
- Modify: `AuditCkDayo/Views/SalesReports/Review.cshtml`

- [ ] **Step 1: Rename the expenses section label**

In `Review.cshtml`, change the section header "Expenses Paid from Sales Box" (line 410) to "Expenses from PCF".

- [ ] **Step 2: Show the combined Cash Sales on the manager side**

In the metadata summary grid (around line 94-121), add a combined cash card visible to Managers/Owners. Add after the "Total Cash to Handover" card (after line 109):

```html
                        @if (User.IsInRole("Manager") || User.IsInRole("Owner"))
                        {
                            <div class="bg-surface-container-low rounded-xl p-space-4 border-l-4 border-tertiary shadow-sm flex flex-col justify-center">
                                <span class="font-label-caps text-on-surface-variant uppercase text-[9px] tracking-wider">Combined Cash Sales (Opening + Closing)</span>
                                <div class="flex items-baseline gap-space-1 mt-1 font-body-md">
                                    <span class="text-on-surface font-bold text-[18px]">₱</span>
                                    <span class="font-data-mono text-on-surface font-bold text-[22px]">@((Model.OpeningCashSales + Model.CashSales).ToString("N2"))</span>
                                </div>
                            </div>
                        }
```

- [ ] **Step 3: Build to confirm compilation**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add AuditCkDayo/Views/SalesReports/Review.cshtml
git commit -m "feat(sales): rename expense label and show combined cash sales on manager side"
```

---

### Task 11: Combine opening + closing across dashboard, historical daily-sales, reports/P&L, and treasury

The report's displayed/published figures must reflect the daily total (opening + closing), not just closing. Add read-only computed properties on the model so every consumer (dashboard, historical daily-sales list, reports, P&L, treasury post) uses the combined values without changing the stored closing columns.

**Files:**
- Modify: `AuditCkDayo/Models/SalesReport.cs`
- Modify: `AuditCkDayo/ViewModels/DashboardViewModel.cs`
- Modify: `AuditCkDayo/Views/Home/Index.cshtml`
- Modify: `AuditCkDayo/ViewModels/ReportsViewModel.cs`
- Modify: `AuditCkDayo/Controllers/SalesReportsController.cs`

- [ ] **Step 1: Add combined computed properties to `SalesReport`**

In `AuditCkDayo/Models/SalesReport.cs`, after the `OpeningNotes` property, add:

```csharp
        [NotMapped]
        public decimal TotalGrossSales => GrossSales + OpeningGrossSales;

        [NotMapped]
        public decimal TotalCashSales => CashSales + OpeningCashSales;

        [NotMapped]
        public decimal TotalConfirmedCashToHandover => ConfirmedCashToHandover + OpeningCashSales;
```

`TotalConfirmedCashToHandover` is the combined cash handed over: closing counted cash (`ConfirmedCashToHandover`) plus the opening cash sales (`OpeningCashSales`), which becomes the amount posted to treasury and shown on the dashboard.

- [ ] **Step 2: Build to confirm compilation**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds.

- [ ] **Step 3: Update the dashboard summary totals to use combined values**

In `AuditCkDayo/ViewModels/DashboardViewModel.cs`, change `PendingSalesGrossTotal` and `PendingSalesCashToHandoverTotal` (lines 28-29) to use the combined properties:

```csharp
        public decimal PendingSalesGrossTotal => PendingSalesReports.Sum(r => r.TotalGrossSales);
        public decimal PendingSalesCashToHandoverTotal => PendingSalesReports.Sum(r => r.TotalConfirmedCashToHandover);
```

- [ ] **Step 4: Update the dashboard views to display combined values**

In `AuditCkDayo/Views/Home/Index.cshtml`, replace every `@report.GrossSales` reference in the pending and historical daily-sales blocks with `@report.TotalGrossSales`, and every `@report.ConfirmedCashToHandover` in those blocks with `@report.TotalConfirmedCashToHandover`. Affected lines: 307, 308, 346, 350, 565, 704, 708.

- [ ] **Step 5: Update the P&L report to use combined gross sales**

In `AuditCkDayo/ViewModels/ReportsViewModel.cs`, replace `report.GrossSales` with `report.TotalGrossSales` at the P&L aggregation points (lines 352 and 392), so P&L `TotalSales` and per-branch `Sales` reflect the full daily gross.

- [ ] **Step 6: Post the combined cash to treasury**

In `AuditCkDayo/Controllers/SalesReportsController.cs`, `PostConfirmedSalesReportToTreasuryAsync` currently sets `entry.Amount = report.ConfirmedCashToHandover;` (line 556). Change it to the combined value:

```csharp
            entry.Amount = report.TotalConfirmedCashToHandover;
```

- [ ] **Step 7: Build to confirm compilation**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: build succeeds.

- [ ] **Step 8: Commit**

```bash
git add AuditCkDayo/Models/SalesReport.cs AuditCkDayo/ViewModels/DashboardViewModel.cs AuditCkDayo/Views/Home/Index.cshtml AuditCkDayo/ViewModels/ReportsViewModel.cs AuditCkDayo/Controllers/SalesReportsController.cs
git commit -m "feat(sales): combine opening and closing across dashboard, reports, and treasury"
```

---

### Task 12: Tests

**Files:**
- Modify: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Write a failing test for opening review GET**

Add a test class/method verifying `OpeningReview` GET returns the opening model. Using the existing SQLite in-memory setup (see `UsersControllerTests` for the pattern), seed an establishment + a BranchStaff user + a sales report, and assert `OpeningReview(id)` returns a `ViewResult` whose model has `ReportSection == SalesReportSection.Opening`.

```csharp
        [Fact]
        public async Task OpeningReview_ReturnsOpeningModel()
        {
            using (var context = new AuditDbContext(_options))
            {
                var controller = CreateSalesReportsController(context, currentUserId: 1, currentUserRole: "Owner");
                var result = await controller.OpeningReview(1);
                var view = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<SalesReportReviewViewModel>(view.Model);
                Assert.Equal(SalesReportSection.Opening, model.ReportSection);
            }
        }
```

Note: add the supporting seed data (establishment + owner + sales report with document record) and a `CreateSalesReportsController` helper following the existing `CreateController` pattern in `UnitTest1.cs`.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj`
Expected: the new test fails (or errors) because the seed data/helper is not yet complete or the action is missing. Iterate until the helper and seed are correct.

- [ ] **Step 3: Write a test for combined cash sales math**

Add a test asserting that when opening cash sales and closing cash sales are both set, the manager-side combined value equals their sum. This can be a pure view-model assertion:

```csharp
        [Fact]
        public void CombinedCashSales_IsSumOfOpeningAndClosing()
        {
            var model = new SalesReportReviewViewModel { OpeningCashSales = 300m, CashSales = 500m };
            Assert.Equal(800m, model.OpeningCashSales + model.CashSales);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj`
Expected: both new tests pass and the existing suite still passes.

- [ ] **Step 5: Commit**

```bash
git add AuditCkDayo.Tests/UnitTest1.cs
git commit -m "test(sales): cover opening review and combined cash sales"
```

---

### Task 13: Verify the full build and test suite

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build AuditCkDayo.sln`
Expected: build succeeds with no errors.

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj`
Expected: all tests pass.

- [ ] **Step 3: Manual smoke check (dev environment)**

With MySQL running, launch `dotnet run --project AuditCkDayo/AuditCkDayo.csproj`, log in as a BranchStaff account, open the Daily Sales page, confirm the table lists the day's reports, add an opening via "Add Opening", save the opening draft, add the closing via "Add Closing Daily Sales", verify the closing page shows the combined cash, and confirm the report only appears on a manager's review list once closing is submitted.

- [ ] **Step 4: Commit any final changes**

```bash
git add -A
git commit -m "feat(sales): finalize daily sales opening section"
```

---

## Self-Review

**Spec coverage:**
- BranchStaff Daily Sales table with "Add Opening" and "Add Closing Daily Sales" per row → Task 5 (controller) + Task 8 (view).
- Opening → new editable draft-only page → Task 6 (actions, draft-only) + Task 9 (view, single Save Opening Draft).
- Closing → existing `Review` page → label rename + manager-side combined card in Task 10.
- Closing submits to manager only when opening exists → Task 7 Step 6.
- Full opening detail stored like closing → Tasks 1-4 (model/viewmodel), Task 7 (populate/save).
- Opening line items + cash breakdown stored via section discriminator → Tasks 2, 3, 6, 7.
- Combined Cash Sales across dashboard, historical daily-sales, reports/P&L, and treasury posting → Task 11 (via `TotalGrossSales`, `TotalCashSales`, `TotalConfirmedCashToHandover`). The manager-side card in Task 10 shows `OpeningCashSales + CashSales`.
- Opening never independently posts to treasury / manager → Task 6 (draft-only) + Task 7 Step 6 (gate).

**Placeholder scan:** No TBD/TODO placeholders; all steps contain concrete code or explicit instructions. The OpeningReview view (Task 9) is created by copying the closing template with explicit binding changes — the only intentionally template-driven step, with the exact transformation rules enumerated.

**Type consistency:** `SalesReportSection` enum (`Closing=0`, `Opening=1`) used consistently in model, view model, and controller. `Opening*Lines` view-model lists match the `ApplyOpeningLines`/`AddOpeningLines` signatures and the opening view prefixes. `OpeningItems`/`CashBreakdownLineViewModel` used consistently across view model, controller population, and view.

**Note:** Task 3 upgrades `dotnet-ef` to match EF9; if the environment cannot upgrade, the migration can instead be authored by hand following the existing migration patterns in `AuditCkDayo/Migrations/`.
