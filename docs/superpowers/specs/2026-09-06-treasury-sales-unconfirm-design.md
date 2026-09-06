# Treasury Sales Unconfirm Design

## Goal

Allow managers to unlock a confirmed daily sales report from the existing Treasury entry edit flow when the branch needs to fix dates, values, or reconciliation text after confirmation.

## Current Behavior

Confirmed sales reports post a `CashFlowEntry` into Treasury as a sales cash-in. The report and its `DocumentRecord` are marked `Confirmed`, and normal sales draft saving is blocked. When a branch needs to correct a confirmed report, the current workaround is a manual database update that deletes the linked treasury sales cash-in, resets the sales report/document to `Draft`, clears confirmation fields, and recomputes treasury totals.

## Approved User Flow

1. Manager opens the Treasury daily cash-flow page.
2. Manager clicks the existing Edit button on a sales cash-in entry.
3. The edit page detects that the entry is linked to a confirmed sales report.
4. The edit page shows an `Unconfirm Sales Report` action.
5. Manager confirms the warning dialog.
6. The system removes the linked sales cash-in from Treasury.
7. The system resets the linked sales report/document to editable draft state.
8. The system recomputes the affected Treasury day totals.
9. The system redirects back to the same Treasury date page.
10. Branch edits the sales report.
11. The report returns to manager review through the existing sales workflow.
12. Manager confirms the corrected report again.
13. Existing confirmation logic reposts the corrected sales cash-in.

## Authorization

The action is available only to users already authorized for Treasury management: `Owner`, `Manager`, or `Admin` through `TreasuryController` authorization.

The action must not be available to branch staff directly. Branch staff edit only after a manager/admin/owner unlocks the report.

## Visibility Rules

Show the `Unconfirm Sales Report` button on `Treasury/EditEntry` only when all conditions are true:

- The `CashFlowEntry.Direction` is `In`.
- The `CashFlowEntry.Category` is `Sales`.
- The `CashFlowEntry.SourceDocumentId` is not null.
- The linked `DocumentRecord.DocumentType` is `DailySalesReport`.
- A linked `SalesReport` exists for that `DocumentRecordId`.
- The linked `SalesReport.Status` is `Confirmed` or the linked `DocumentRecord.ReviewStatus` is `Confirmed`.
- The parent `TreasuryCashFlow.Status` is not `Closed`.

If the Treasury day is closed, do not show the action. Closed days stay locked.

## Data Changes

On unconfirm:

### `CashFlowEntries`

Delete the current sales cash-in entry:

- `Id = selected entry id`
- `Category = Sales`
- `SourceDocumentId = linked document id`

### `SalesReports`

Reset the linked report:

- `Status = Draft`
- `ConfirmedByUserId = null`
- `ConfirmedAt = null`

Keep the report's business date, handover date, sales values, uploaded images, closing/opening lines, and other entered content unchanged.

### `DocumentRecords`

Reset the linked document:

- `ReviewStatus = Draft`
- `ConfirmedByUserId = null`
- `ConfirmedAt = null`

Keep document image URL, OCR status, OCR raw JSON, uploader, and upload timestamp unchanged.

### `TreasuryCashFlows`

Recompute the affected flow after deleting the cash-in:

- `TotalCashIn = sum(In entries)`
- `TotalCashOut = sum(Out entries)`
- `NetCashFlow = StartingBalance + TotalCashIn`
- `ClosingBalance = NetCashFlow - TotalCashOut`

## Transaction Requirements

The unconfirm action must run in a single database transaction:

1. Load the selected `CashFlowEntry` with its `TreasuryCashFlow`.
2. Validate that the user may operate on the entry.
3. Validate the flow is not closed.
4. Validate the entry is a linked sales cash-in.
5. Load the linked `DocumentRecord` and `SalesReport`.
6. Delete the sales cash-in entry.
7. Reset `SalesReport` confirmation fields/status.
8. Reset `DocumentRecord` confirmation fields/status.
9. Recompute treasury flow totals.
10. Save changes.
11. Commit.

If any validation fails, no partial change should persist.

## UI Requirements

On `Treasury/EditEntry`, keep the existing edit form.

Add a separate danger section below the edit form when the visibility rules pass:

- Heading: `Sales report confirmation`
- Text: `This cash-in came from a confirmed sales report. Unconfirming it removes this cash-in from Treasury and lets the branch edit the sales report again.`
- Button: `Unconfirm Sales Report`
- Button style: destructive/danger treatment.
- Confirmation message: `This will remove the sales cash-in from Treasury and send the sales report back to draft so the branch can edit it. The manager must confirm it again after editing. Continue?`

## Redirects and Messages

On success, redirect to:

`Treasury/Index?date=<same treasury cash-flow date>`

Success message:

`Sales report unconfirmed. Treasury cash-in was removed and the branch can edit the report again.`

Failure messages:

- Closed flow: `This entry belongs to a closed/locked treasury day and cannot be unconfirmed.`
- Missing entry: `Treasury entry not found.`
- Not sales-linked: `Only linked sales cash-in entries can be unconfirmed from Treasury.`
- Missing sales report: `Linked sales report not found.`

## Files Expected to Change Later

No implementation is included in this spec. Expected implementation files:

- `AuditCkDayo/Controllers/TreasuryController.cs`
  - Add a POST action for unconfirming linked sales reports from Treasury.
  - Extend the GET edit action to load/display linked sales-report state.

- `AuditCkDayo/Views/Treasury/EditEntry.cshtml`
  - Add the conditional danger-section button.

- `AuditCkDayo.Tests/UnitTest1.cs` or a new focused treasury test file following existing test conventions
  - Add tests for unconfirm behavior, authorization/locking behavior, cash-flow deletion, and total recomputation.

## Acceptance Criteria

- A confirmed sales cash-in entry from a sales report shows the unconfirm action on Treasury Edit Entry.
- Manual cash-in entries do not show the action.
- Non-sales entries do not show the action.
- Closed treasury days do not show or allow the action.
- Unconfirm deletes the linked sales cash-in entry.
- Unconfirm resets the linked `SalesReport` to `Draft`.
- Unconfirm resets the linked `DocumentRecord` to `Draft`.
- Unconfirm clears confirmation user/date fields on both records.
- Unconfirm recomputes the affected Treasury day totals.
- Redirect returns to the same Treasury date page.
- The branch can edit the sales report afterward through the existing sales report edit flow.
- Reconfirming the edited report uses existing confirmation logic and reposts the corrected cash-in.

## Non-Goals

- No branch-staff direct unconfirm button.
- No new approval-request workflow.
- No new database tables.
- No audit-history table in this first version.
- No changes to sales report confirmation posting logic except ensuring it continues to repost after unconfirm/edit.
- No change to closed Treasury day locking behavior.

## Self-Review

- Placeholder scan: no TBD/TODO placeholders remain.
- Scope check: one focused feature, limited to Treasury edit flow and linked sales report unlock.
- Data consistency: cash-in deletion, report reset, document reset, and total recompute are covered in one transaction.
- Ambiguity resolved: redirect goes back to the Treasury page; manager/owner/admin only; closed days stay locked.
