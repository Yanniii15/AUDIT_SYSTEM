# Gemini TODO: Daily Sales and PCF Workflow Corrections

## Context
Work in this repo/worktree:

```text
C:\Users\John Salvamante\Desktop\FINAL AUDITING SYSTEM\.worktrees\treasury-remodel
```

The user clarified two important business rules:

1. **Daily Sales is not a simple one-image form.** It should work like the existing **New Audit** upload/review flow: branch staff can upload/take multiple pictures, preview the images, OCR/extract rows, edit the listed sales rows, then confirm.
2. **New PCF / New Audit must be available to everyone** because any user may buy items and need to submit receipts for auditing. BranchStaff should not be blocked from New Audit.

## Current status
- A design spec was added for the corrected Daily Sales flow:

```text
docs/superpowers/specs/2026-08-10-daily-sales-new-audit-flow-design.md
```

- PCF/New Audit access is being restored so BranchStaff can use New Audit again while still having a separate Daily Sales link.

## Required end state

### Navigation
BranchStaff sidebar should show both:

```text
New Audit
Verify Deliveries
Daily Sales
Reports
```

Other roles should keep their appropriate existing navigation.

### New Audit / PCF expense flow
- `AuditsController.Upload` and `AuditsController.ProcessUpload` must allow:

```text
Buyer, Owner, Manager, BranchStaff, Admin
```

- This flow remains for receipt/PCF expense auditing when someone buys items.

### Daily Sales flow
Replace the current plain Daily Sales upload with a flow copied from New Audit, but for sales reports:

1. `/SalesReports/Upload`
   - Allows multiple image upload, up to 5 images.
   - Camera/mobile friendly via `accept="image/*"` and `multiple`.
   - Drag/drop zone like New Audit.
   - Thumbnail preview grid.
   - Rotate/remove/reorder controls like New Audit.
   - Keeps Daily Sales-specific fields where needed: operating branch, business date, handover date, cashier name.

2. Sales report processing
   - Saves all uploaded images for one sales report.
   - Keeps first image as the primary image if needed for existing compatibility.
   - Stores all image URLs so the review page can show thumbnails.
   - Runs OCR. If multi-image sales OCR is not fully supported, parse the first image and allow manual correction from all visible uploaded images.
   - OCR failure must still allow manual review.

3. `/SalesReports/Review/{id}`
   - Uses New Audit-style layout:
     - left side: large image preview;
     - thumbnails below;
     - click thumbnail to change preview;
     - right side: editable sales reconciliation form.
   - Shows extracted sales rows in an editable table.
   - User can add/remove/edit rows.
   - Totals update from rows where possible.
   - Save Draft keeps work in progress.
   - Confirm posts one treasury cash-in entry.

4. Treasury posting
   - Confirming one sales report creates/updates exactly one `CashFlowEntry`:

```text
Direction = In
Category = Sales
SourceDocumentId = sales report document
EstablishmentId = selected branch
Amount = ConfirmedCashToHandover
```

- Re-confirming or saving should not create duplicate cash-in entries.
- Save Draft must not downgrade a confirmed report.

## Tests to add/update

### PCF/New Audit access
- Test `AuditsController.Upload` allows `BranchStaff`.
- Test `AuditsController.ProcessUpload` allows `BranchStaff`.
- Existing `BranchStaffNavigationPolicyTests` should expect BranchStaff access, not denial.

### Daily Sales multi-image upload
Add focused tests proving:

- Upload rejects zero files.
- Upload rejects more than 5 files.
- Upload accepts multiple valid image files and creates one `SalesReport`.
- All uploaded image URLs are preserved for the review screen.
- BranchStaff cannot upload for another establishment.
- Review model exposes all image URLs.
- Confirm still posts exactly one treasury cash-in entry.

## Manual browser smoke test
Use seeded account:

```text
staff@test.com / Password123!
```

Expected:

1. Login as BranchStaff.
2. Sidebar shows both **New Audit** and **Daily Sales**.
3. Open `/Audits/Upload`:
   - page should load, not Access Denied.
4. Open `/SalesReports/Upload`:
   - page should look and behave like New Audit upload;
   - select multiple images;
   - thumbnails appear;
   - upload leads to review page;
   - review page shows large preview + thumbnails;
   - extracted/editable sales rows are visible.

## Verification commands
Run focused tests first:

```powershell
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --filter "BranchStaffNavigationPolicyTests|SalesReportUsabilityTests"
```

Then run the local suite excluding the live Gemini API test if it is rate-limited:

```powershell
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --filter "FullyQualifiedName!~GoogleGeminiOcrService_IntegratesWithRealApiSuccessfully"
```

Note: the live Gemini OCR integration test may fail with HTTP 429 if the API quota is rate-limited. That failure is external to these workflow changes.

## Do not do
- Do not remove New Audit from BranchStaff.
- Do not merge Daily Sales into New Audit; they are separate flows.
- Do not let Daily Sales confirmation create duplicate treasury cash-in entries.
- Do not push to GitHub unless explicitly asked.
