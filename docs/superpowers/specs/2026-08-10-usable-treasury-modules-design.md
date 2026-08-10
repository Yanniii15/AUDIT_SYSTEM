# Usable Treasury Modules Design

## Problem
The treasury remodel currently has working models, migrations, tests, routes, and navigation, but the browser modules are skeleton pages. Users can open Coverage, Treasury, Sales Reports, PCF Release, and Audit Settlement, but cannot create records, review data, or see meaningful tables.

## Goal
Turn the skeleton pages into usable v1 screens without expanding into the later advanced workflow phases.

## Scope
- Coverage: Admin/Owner can create coverage assignments and list existing assignments.
- Treasury: Owner/Manager/Admin can view a daily cash-flow dashboard with totals and entries.
- Sales Reports: Owner/Manager/BranchStaff/Admin can upload a sales-report image, review/edit extracted/manual values, save draft, and confirm to treasury cash-in.
- PCF Release: Owner/Manager/Admin can record a PCF release and post it as treasury cash-out.
- Audit Settlement: Owner/Manager/Admin can record settlement math and save confirmed settlement records.

## Non-goals
- No mobile redesign.
- No advanced OCR parsing beyond using the existing `IOcrService.ParseSalesReportAsync` result when available and allowing manual correction.
- No adjustment-record workflow after confirmation.
- No GitHub push or deployment movement.

## Architecture
Keep the existing ASP.NET Core MVC structure. Use current domain models and DbContext. Controllers perform simple v1 orchestration: validate form input, create/update records, recompute totals, and render tables. Views use the existing sidebar/header visual language and Tailwind-style utility classes already present in the app.

## Data rules
- Confirming a sales report creates or updates a daily `TreasuryCashFlow` for the handover date and writes one `CashFlowEntry` with Direction `In`, Category `Sales`, SourceDocumentId, EstablishmentId, and amount equal to `ConfirmedCashToHandover`.
- Recording a PCF release creates or updates a daily `TreasuryCashFlow` for the release date and writes one `CashFlowEntry` with Direction `Out`, Category `PcfRelease`, amount equal to release amount, and links it to `PcfRelease.CashFlowEntryId`.
- Settlement save computes ExpectedChange and ShortOverAmount through `AuditSettlement.Recompute()` before persistence.
- Coverage creation requires different covered and covering managers and inclusive valid date range.

## UI rules
- Empty pages must become actionable forms plus tables.
- Every save action uses anti-forgery validation.
- Success/failure messages use TempData and existing alert conventions.
- Keep forms conservative: required fields, branch/manager dropdowns, currency inputs, date inputs, notes/purpose text areas.

## Verification
- Add controller/model tests for coverage validation, sales confirmation posting, PCF release posting, and settlement persistence math.
- Run focused tests after each module.
- Run `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release`.
- Start the app and browser-smoke the five module pages.