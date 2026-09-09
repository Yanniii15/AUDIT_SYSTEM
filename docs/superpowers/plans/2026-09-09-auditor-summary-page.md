# Auditor Summary Page & Reports Role-Gating Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a dedicated `/Audits/Summary` page for Auditors and Owners with beginning balance roll-forward, manager cash in/out, buyer liquidation, and PCF matrix; restrict `/Reports` to Owners and Managers; and deploy to Railway.

**Architecture:** ASP.NET Core MVC with EF Core MySQL. A dedicated `Summary` action in `AuditsController` and `AuditSummaryViewModel` encapsulates the operational audit dataset (PCF releases with Beginning Balance, buyer liquidation, cash in/out, branch allocations) with strict role authorization (`Owner,Auditor,Admin`). `ReportsController` revokes `Auditor` access.

**Tech Stack:** .NET 9, ASP.NET Core MVC, Entity Framework Core 9, Pomelo MySQL, ClosedXML, Tailwind-style CSS, Railway CLI.

---

### Task 1: ViewModels for Audit Summary

**Files:**
- Create: `AuditCkDayo/ViewModels/AuditSummaryViewModel.cs`

- [ ] **Step 1: Write `AuditSummaryFilterViewModel` and `AuditSummaryViewModel`**

Define `AuditSummaryFilterViewModel` (StartDate, EndDate, EstablishmentId, BuyerId, ManagerId, BeginningBalanceOverride) and `AuditSummaryViewModel` (Filter, BeginningBalance, IsBeginningBalanceOverridden, PcfMatrix, BuyerAudits, ManagerCashInRows, ManagerCashOutRows, BranchAudit).

- [ ] **Step 2: Build the project to verify compilation**

Run: `dotnet build AuditCkDayo/AuditCkDayo.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 2: Controller & Calculation Logic in `AuditsController`

**Files:**
- Modify: `AuditCkDayo/Controllers/AuditsController.cs`
- Modify: `AuditCkDayo/Controllers/ReportsController.cs`

- [ ] **Step 1: Restrict `ReportsController`**
Change `[Authorize(Roles = "Owner,Manager,Auditor,Buyer")]` to `[Authorize(Roles = "Owner,Manager,Admin")]`.

- [ ] **Step 2: Implement `AuditsController.Summary` Action**
Add `[HttpGet] [Authorize(Roles = "Owner,Auditor,Admin")] public async Task<IActionResult> Summary(AuditSummaryFilterViewModel filter)` in `AuditsController.cs`:
- Resolve `StartDate` and `EndDate`.
- Query prior period unliquidated / handed change to compute default `BeginningBalance`.
- Populate PCF Release matrix with the `BEGINNING` row under `OTHERS` on the start date.
- Calculate:
  - `Total PC = BeginningBalance + Releases`
  - `Total Expenses = Sum(Approved expenses)`
  - `Actual Change = Total PC - Total Expenses`
  - `Handed Change = input or sum of actual change returned`
  - `Short / Over = Handed Change - Actual Change`
- Query Manager Cash In rows (`CashFlowEntries` with `Direction == In`).
- Query Manager Cash Out rows (`CashFlowEntries` with `Direction == Out`).
- Query Buyer Audits and Branch Allocation expenses.
- Populate dropdown ViewBags (`Establishments`, `Buyers`, `Managers`).

- [ ] **Step 3: Build and run existing tests**
Run: `dotnet build AuditCkDayo.sln`

---

### Task 3: Razor View `Audits/Summary.cshtml` & Navigation

**Files:**
- Create: `AuditCkDayo/Views/Audits/Summary.cshtml`
- Modify: `AuditCkDayo/Views/Shared/_Layout.cshtml`

- [ ] **Step 1: Create `Summary.cshtml`**
Implement the clean view:
- Filter bar (Date range, Establishment, Buyer, Manager, Beginning Balance input, Filter button, Excel exports).
- Metric summary cards (`TOTAL PC`, `TOTAL EXP`, `ACTUAL CHANGE`, `HANDED CHANGE`, `SHORT / OVER`).
- PCF Release Matrix table (Date rows, Custodian columns, `OTHERS` column showing `₱... BEGINNING` on start date, Totals row).
- Buyer Liquidation cards with issuances, itemized expenses with green checkmarks for receipts, and bottom variance strips.
- Manager Cash In Details table & Manager Cash Out Details table.
- Branch Audit / Expense Allocations table.
- Hidden receipt preview modal for auditing lines.

- [ ] **Step 2: Update Sidebar Navigation in `_Layout.cshtml`**
- For `Auditor`: Show **"Audit Summary"** (`asp-controller="Audits" asp-action="Summary"`), remove "Reports".
- For `Owner`: Keep "Reports", add "Audit Summary".
- For `Manager`: Keep "Reports", do not show "Audit Summary".

---

### Task 4: Unit Testing & Verification

**Files:**
- Create: `AuditCkDayo.Tests/AuditSummaryTests.cs`

- [ ] **Step 1: Add Unit Tests**
Test:
- `AuditsController.Summary` authorization attributes (`Owner`, `Auditor`, `Admin`).
- `ReportsController` role authorization (verifying `Auditor` is no longer authorized).
- Beginning Balance roll-forward calculation into `Total PC` and `Actual Change`.
- Short/Over calculation: `Handed Change - Actual Change`.

- [ ] **Step 2: Run Tests**
Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj`
Expected: All tests pass.

---

### Task 5: Build & Railway Deployment

- [ ] **Step 1: Verify git status and local build**
- [ ] **Step 2: Deploy to Railway via `railway up`**
- [ ] **Step 3: Verify deployment status and test live endpoint**
Run `railway status` and verify `https://makbiecompanies.dev`.
