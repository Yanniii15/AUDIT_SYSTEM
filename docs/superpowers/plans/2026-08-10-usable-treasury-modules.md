# Usable Treasury Modules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace skeleton treasury-remodel pages with usable v1 forms, tables, and persistence flows.

**Architecture:** Keep ASP.NET Core MVC controllers/views. Reuse existing models and EF Core DbContext. Add focused tests for controller/model behavior, then implement minimal working forms and persistence.

**Tech Stack:** ASP.NET Core MVC, EF Core MySQL provider, xUnit, existing Tailwind-style utility classes.

---

## File Structure

### Modify
- `AuditCkDayo/Controllers/CoverageController.cs` — list and create temporary manager coverage.
- `AuditCkDayo/Views/Coverage/Index.cshtml` — coverage form and table.
- `AuditCkDayo/Controllers/TreasuryController.cs` — daily dashboard, PCF release save, settlement save.
- `AuditCkDayo/Views/Treasury/Index.cshtml` — daily cash-flow totals and entries table.
- `AuditCkDayo/Views/Treasury/ReleasePcf.cshtml` — release form.
- `AuditCkDayo/Views/Treasury/Settlement.cshtml` — settlement form and computed preview values.
- `AuditCkDayo/Controllers/SalesReportsController.cs` — upload image, create document/report, review, save draft, confirm to cash flow.
- `AuditCkDayo/Views/SalesReports/Upload.cshtml` — upload form.
- `AuditCkDayo/Views/SalesReports/Review.cshtml` — editable review/confirm form.
- `AuditCkDayo/ViewModels/TreasuryCashFlowViewModel.cs` — add selected date, entries, optional lists if needed.
- `AuditCkDayo/ViewModels/PcfReleaseViewModel.cs` — add dropdown support fields if needed.
- `AuditCkDayo/ViewModels/AuditSettlementViewModel.cs` — add dropdown support fields if needed.
- `AuditCkDayo/ViewModels/SalesReportReviewViewModel.cs` — add missing upload/review helper fields if needed.
- `AuditCkDayo.Tests/UnitTest1.cs` — add focused tests.

---

## Task 1: Coverage Management

**Files:**
- Modify: `AuditCkDayo/Controllers/CoverageController.cs`
- Modify: `AuditCkDayo/Views/Coverage/Index.cshtml`
- Test: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Write failing tests**

Add tests proving coverage validation rejects same manager and invalid dates, and valid coverage persists via EF in-memory/SQLite context.

- [ ] **Step 2: Run tests**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --filter CoverageUsabilityTests
```
Expected: FAIL before controller/service behavior exists.

- [ ] **Step 3: Implement controller**

Add POST `Index` or `Create` with `[ValidateAntiForgeryToken]`. Load Manager users for dropdowns. Require covered and covering manager to differ, EndDate >= StartDate, and CreatedByUserId from claims. Save `ManagerCoverage`.

- [ ] **Step 4: Implement view**

Render create form with covered manager, covering manager, start/end dates, scope, reason, active flag, submit button, and existing coverage table.

- [ ] **Step 5: Verify**

Run targeted tests; commit:
```bash
git add AuditCkDayo/Controllers/CoverageController.cs AuditCkDayo/Views/Coverage/Index.cshtml AuditCkDayo.Tests/UnitTest1.cs
git commit -m "feat: make coverage module usable"
```

---

## Task 2: Treasury Dashboard

**Files:**
- Modify: `AuditCkDayo/Controllers/TreasuryController.cs`
- Modify: `AuditCkDayo/Views/Treasury/Index.cshtml`
- Modify: `AuditCkDayo/ViewModels/TreasuryCashFlowViewModel.cs`
- Test: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Write failing tests**

Add tests proving a daily treasury view model includes entries and recomputed totals for a selected date.

- [ ] **Step 2: Run tests**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --filter TreasuryDashboardTests
```
Expected: FAIL before dashboard loading is implemented.

- [ ] **Step 3: Implement dashboard loading**

`TreasuryController.Index(DateTime? date)` loads `TreasuryCashFlow` for selected date including Entries, Establishment, CostCenter, RelatedUser, SourceDocument. If none exists, show zero totals and empty entries. Recompute displayed totals from entries.

- [ ] **Step 4: Implement view**

Render date filter, cards for starting balance, cash in, cash out, net cash flow, closing balance, and an entries table with direction, category, amount, branch/cost center/user, source document, notes.

- [ ] **Step 5: Verify and commit**

Run targeted tests and commit:
```bash
git add AuditCkDayo/Controllers/TreasuryController.cs AuditCkDayo/Views/Treasury/Index.cshtml AuditCkDayo/ViewModels/TreasuryCashFlowViewModel.cs AuditCkDayo.Tests/UnitTest1.cs
git commit -m "feat: show treasury cash flow dashboard"
```

---

## Task 3: Sales Report Upload and Confirmation

**Files:**
- Modify: `AuditCkDayo/Controllers/SalesReportsController.cs`
- Modify: `AuditCkDayo/Views/SalesReports/Upload.cshtml`
- Modify: `AuditCkDayo/Views/SalesReports/Review.cshtml`
- Modify: `AuditCkDayo/ViewModels/SalesReportReviewViewModel.cs`
- Test: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Write failing tests**

Add tests proving confirming a sales report creates a cash-in entry with category `Sales`, amount equal to `ConfirmedCashToHandover`, establishment, source document, and recomputed daily totals.

- [ ] **Step 2: Run tests**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --filter SalesReportUsabilityTests
```
Expected: FAIL before confirmation posting exists.

- [ ] **Step 3: Implement upload POST**

Accept one image file, validate extension like existing audit upload, save under app data/uploads/sales-reports or existing uploads convention, create `DocumentRecord` with type `DailySalesReport`, call `ParseSalesReportAsync` if possible, create draft `SalesReport`, redirect to Review.

- [ ] **Step 4: Implement review GET/POST**

GET loads report into `SalesReportReviewViewModel`. POST SaveDraft updates editable fields. POST Confirm updates report/document status and posts/updates one treasury cash-in entry for the handover date.

- [ ] **Step 5: Implement views**

Upload view shows branch/date/file fields. Review view shows image link, editable fields, Save Draft and Confirm buttons.

- [ ] **Step 6: Verify and commit**

Run targeted tests and commit:
```bash
git add AuditCkDayo/Controllers/SalesReportsController.cs AuditCkDayo/Views/SalesReports/Upload.cshtml AuditCkDayo/Views/SalesReports/Review.cshtml AuditCkDayo/ViewModels/SalesReportReviewViewModel.cs AuditCkDayo.Tests/UnitTest1.cs
git commit -m "feat: make sales report review usable"
```

---

## Task 4: PCF Release Flow

**Files:**
- Modify: `AuditCkDayo/Controllers/TreasuryController.cs`
- Modify: `AuditCkDayo/Views/Treasury/ReleasePcf.cshtml`
- Modify: `AuditCkDayo/ViewModels/PcfReleaseViewModel.cs`
- Test: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Write failing tests**

Add tests proving saving a PCF release creates `PcfRelease`, creates a treasury cash-out `CashFlowEntry` category `PcfRelease`, links `CashFlowEntryId`, and recomputes daily totals.

- [ ] **Step 2: Run tests**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --filter PcfReleaseUsabilityTests
```
Expected: FAIL before save flow exists.

- [ ] **Step 3: Implement GET/POST**

GET loads receiver/branch dropdowns. POST validates amount > 0 and at least ReceiverUserId or ReceiverName/Establishment context. Save `PcfRelease`, post cash-out entry, recompute flow, redirect to Treasury index for date.

- [ ] **Step 4: Implement view**

Render amount, date, receiver user, receiver name, establishment, purpose fields and submit button.

- [ ] **Step 5: Verify and commit**

Run targeted tests and commit:
```bash
git add AuditCkDayo/Controllers/TreasuryController.cs AuditCkDayo/Views/Treasury/ReleasePcf.cshtml AuditCkDayo/ViewModels/PcfReleaseViewModel.cs AuditCkDayo.Tests/UnitTest1.cs
git commit -m "feat: make pcf release usable"
```

---

## Task 5: Audit Settlement Flow

**Files:**
- Modify: `AuditCkDayo/Controllers/TreasuryController.cs`
- Modify: `AuditCkDayo/Views/Treasury/Settlement.cshtml`
- Modify: `AuditCkDayo/ViewModels/AuditSettlementViewModel.cs`
- Test: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Write failing tests**

Add tests proving saving a settlement persists recomputed ExpectedChange and ShortOverAmount and links responsible/processed users.

- [ ] **Step 2: Run tests**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --filter AuditSettlementUsabilityTests
```
Expected: FAIL before save flow exists.

- [ ] **Step 3: Implement GET/POST**

GET loads optional PCF releases and managers. POST validates nonnegative amounts, uses current user as ProcessedByUserId, uses selected responsible manager or current user fallback, calls `Recompute()`, saves settlement.

- [ ] **Step 4: Implement view**

Render pcf release selector, receiver name, responsible manager, amounts, computed preview area, and submit button.

- [ ] **Step 5: Verify and commit**

Run targeted tests and commit:
```bash
git add AuditCkDayo/Controllers/TreasuryController.cs AuditCkDayo/Views/Treasury/Settlement.cshtml AuditCkDayo/ViewModels/AuditSettlementViewModel.cs AuditCkDayo.Tests/UnitTest1.cs
git commit -m "feat: make audit settlement usable"
```

---

## Task 6: Final Verification

- [ ] Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release
```
Expected: PASS.

- [ ] Start app:
```bash
dotnet run --project AuditCkDayo/AuditCkDayo.csproj --urls http://127.0.0.1:5088
```
Expected: app starts and migrations apply.

- [ ] Browser smoke:
```text
/Coverage shows form and table.
/Treasury shows totals and entries table.
/SalesReports/Upload shows upload form.
/Treasury/ReleasePcf shows release form.
/Treasury/Settlement shows settlement form.
```

- [ ] Commit if final-only changes exist; otherwise report final verification evidence.
