# Daily Sales New-Audit Flow Design

## Problem
The current Daily Sales page is a plain one-image form. Branches actually need the same interaction style as New Audit: upload/take multiple pictures, preview them, OCR the pages, list extracted sales lines, then edit/confirm the result. The current one-image intake does not match the branch workflow.

## Decision
Use the New Audit upload/review pattern for Daily Sales, but map the extracted rows to sales-report fields instead of expense-audit claims.

## Users and access
- BranchStaff can open Daily Sales and upload reports only for their assigned operating branch.
- Owner, Manager, and Admin can also use the page for allowed branches.
- BranchStaff remains blocked from New Audit receipt-audit submission.

## Upload screen
- `/SalesReports/Upload` keeps the Daily Sales purpose and branch/date fields.
- Replace the single file input with the New Audit-style drag/drop and camera-friendly multi-file picker.
- Accept up to 5 PNG/JPG/JPEG/WEBP images.
- Show image thumbnails, remove buttons, rotate controls, and ordering controls before upload.
- Submit all selected images in one request.

## Review screen
- `/SalesReports/Review/{id}` uses the New Audit review layout:
  - left panel: large image preview plus thumbnails for all uploaded sales-report images;
  - right panel: editable reconciliation form.
- The right panel lists extracted sales lines in an editable table, like New Audit line items.
- Rows must be editable/removable, and users can add missing rows manually.
- Totals must update from line rows where possible, while still allowing explicit sales-report summary fields needed by treasury.

## Data model
- Keep `SalesReport` as the daily report header.
- Store multiple uploaded image URLs for one `SalesReport`. Preferred minimal change: add a JSON/string-backed list field on `SalesReport` for image URLs while keeping `DocumentRecord.ImageUrl` as the primary/first image for compatibility.
- Store extracted/editable sales lines in existing `CashBreakdownLine` rows when they represent denomination/cash lines, or add a dedicated sales-line model only if the existing row shape cannot represent the New Audit-style list.
- Confirming the report still creates or updates one treasury `CashFlowEntry` with category `Sales` and amount equal to confirmed cash to handover.

## OCR behavior
- Feed all uploaded images to the sales-report OCR path when supported.
- If the current OCR service supports only one stream for sales reports, parse the first image and preserve all images for review. The branch can manually add/correct rows from the remaining images.
- OCR failure must not block manual review.

## Validation and safety
- Require at least one image and at most five.
- Reject invalid image formats and empty files.
- Preserve existing BranchStaff establishment scoping.
- Confirming must remain idempotent: one confirmed report creates/updates one sales cash-in entry, not duplicates.
- Confirmed reports cannot be silently downgraded by Save Draft.

## Verification
- Add tests for multi-image upload creating one sales report with multiple image URLs.
- Add tests that BranchStaff remains scoped to their assigned branch.
- Add tests that review model exposes all image URLs and editable rows.
- Add tests that confirmation still posts exactly one sales cash-in entry.
- Browser-smoke Daily Sales as BranchStaff: upload multiple images, see thumbnails on review, edit rows, and confirm.
