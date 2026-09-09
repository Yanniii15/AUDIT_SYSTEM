# Auditor Summary Page & Reports Role-Gating Design

## Context & Motivation
Currently, the `/Reports` route in `AuditCkDayo` is accessible to the `Auditor` role and displays full executive financial reports, including Monthly P&L (Total Sales, COGS, OPEX, Fixed Costs, Gross Profit, Net Profit), Branch P&L Comparisons, and full Treasury cash flows alongside operational audit matrices.

The Auditor role's primary function is receipt verification, expense allocation auditing, and PCF liquidation monitoring. Showing executive P&L and company-wide net profit metrics exposes confidential financial data and clutters the auditing interface.

This feature creates a clean, dedicated **Audit Summary** page (`/Audits/Summary`) accessible only to **Owner** and **Auditor** (plus system Admin). The `/Reports` page is restricted to **Owner**, **Manager**, and **Admin**.

In addition, the Audit Summary view incorporates:
1. Multi-factor filtering: **Date Range**, **Establishment**, **Buyer**, **Manager / Custodian**, and **Beginning Balance Override**.
2. **"BEGINNING" Balance** roll-forward logic: On the start date, the initial row displays `BEGINNING` (under `OTHERS` in the matrix), auto-populated from the prior cycle's **Handed Change** (or manual override), which is factored into `Total PC`.
3. Clear terminology:
   - **Total PC** = $\text{Beginning Balance} + \sum(\text{Custodians' Releases})$
   - **Total Expenses** = $\sum(\text{Approved Receipt Deductions})$
   - **Actual Change** = $\text{Total PC} - \text{Total Expenses}$
   - **Handed Change** = physical cash handed over (manual entry)
   - **Short / Over** = $\text{Handed Change} - \text{Actual Change}$
4. **Manager Cash In & Cash Out Logs**: Visible to the auditor and owner for full cash custody traceability.
5. **Branch Audit / Expense Allocations** table.

---

## 1. Access Control & Navigation
- **`ReportsController`**:
  - Update `[Authorize(Roles = "Owner,Manager,Admin")]` (revoke `Auditor` and `Buyer`).
- **`AuditsController.Summary`**:
  - Add `[Authorize(Roles = "Owner,Auditor,Admin")]`.
- **Sidebar (`_Layout.cshtml`)**:
  - For `Auditor`: Replace "Reports" with "Audit Summary" pointing to `asp-controller="Audits" asp-action="Summary"`.
  - For `Owner`: Retain "Reports" and add "Audit Summary".
  - For `Manager`: Retain "Reports"; do not display "Audit Summary".

---

## 2. Data Models & ViewModels
Create `AuditSummaryViewModel` and `AuditSummaryFilterViewModel` (under `AuditCkDayo.ViewModels`):
- **`AuditSummaryFilterViewModel`**:
  - `DateTime? StartDate` (default: current month start or 14 days ago)
  - `DateTime? EndDate` (default: today)
  - `int? EstablishmentId`
  - `int? BuyerId`
  - `int? ManagerId` (Custodian / Treasury handler)
  - `decimal? BeginningBalanceOverride`
- **`AuditSummaryViewModel`**:
  - `AuditSummaryFilterViewModel Filter`
  - `decimal BeginningBalance`
  - `bool IsBeginningBalanceOverridden`
  - `PcfMatrixViewModel PcfMatrix` (includes `BeginningBalance` under the `OTHERS` column on `StartDate`)
  - `List<BuyerAuditReportViewModel> BuyerAudits`
  - `List<TreasuryAuditCashInRowViewModel> ManagerCashInRows`
  - `List<TreasuryAuditCashOutRowViewModel> ManagerCashOutRows`
  - `BranchAuditReportViewModel BranchAudit`
  - SelectLists for `Establishments`, `Buyers`, `Managers`

---

## 3. Calculation & Rolling Balance Logic
1. **Prior Period Handed Change Lookup**:
   - Determine `priorPeriodEndDate = (filter.StartDate ?? Today).AddDays(-1)`.
   - Query prior `SurrenderRequests` or prior recorded `HandedChange` or prior `PcfReleases` balance up to `priorPeriodEndDate`.
   - If user provided `BeginningBalanceOverride.HasValue`, use that value.
2. **Matrix Construction**:
   - Total PC = `BeginningBalance + sum(Releases)`.
   - Actual Change = `Total PC - Total Expenses`.
   - Handed Change = user input (or sum of buyer actual returned change).
   - Short / Over = `Handed Change - Actual Change`.

---

## 4. UI View Components (`Audits/Summary.cshtml`)
- **Filter Bar**: Date range, Establishment, Buyer, Manager dropdowns, Beginning Balance field, "Filter" button, and Excel/Image export buttons.
- **Top Summary Cards**: `TOTAL PC`, `TOTAL EXP`, `ACTUAL CHANGE`, `HANDED CHANGE`, `SHORT / OVER`.
- **PCF Release Matrix**: Grid of dates, custodians (`M. BARBS`, `CHELSEA`, `MAYMAY`, etc.), `OTHERS` with `BEGINNING` row.
- **Buyer Liquidation Cards**: Individual buyer issuances vs. itemized expenses with green checkmarks for receipts.
- **Manager Cash In Details**: Table of cash inflows (Sales, Owner Funding, Surrenders, etc.).
- **Manager Cash Out Details**: Table of cash outflows (Expenses, Supplier, Payroll, etc.).
- **Branch Audit / Expense Allocations**: Allocation table by branch.

---

## 5. Deployment
- Verify all unit tests pass locally (`dotnet test`).
- Test build (`dotnet build`).
- Deploy to Railway via `railway up` and verify production deployment and database.
