# Recommendation Updates

## Purpose

This document records the proposed product and workflow improvements for the auditing and petty-cash system. These are recommendations only and do not represent implemented functionality.

## 1. Mobile Receipt Capture

Buyers should be able to capture or upload receipt images from phones, tablets, laptops, and desktop computers.

Available options should include:

- **Take Photo** — opens a supported device camera and prefers the back-facing camera rather than the front/selfie camera.
- **Choose Image** — selects an existing image from a phone gallery, downloads, device storage, or computer.
- **Choose Images** — selects multiple existing images for one long receipt.

Camera capture must not be mandatory. If camera access is unavailable, unsupported, or denied, image upload should remain usable. Browsers ultimately control camera access, so the interface should allow the buyer to switch cameras if the wrong camera opens.

### Recommended standard receipt flow

1. Buyer selects **Take Photo** or **Choose Image**.
2. The system displays an image preview.
3. The buyer can retake, replace, rotate, remove, or accept the image.
4. The system validates the actual image type, size, and readability.
5. OCR extracts the transaction details.
6. The buyer reviews and corrects all extracted fields before submission.

### Long and multi-page receipts

One receipt may contain up to five ordered images. This supports long receipts that cannot be captured clearly in one photo while keeping upload size and OCR processing bounded.

The Buyer should be able to:

- Capture the top section using the back-facing camera.
- Select **Add Another Photo** for the middle and bottom sections.
- Select multiple images from a phone gallery or computer.
- Combine newly captured photos with uploaded images.
- Reorder, rotate, replace, retake, or remove individual images.
- Preview the complete ordered image set before OCR.
- Add a replacement image without restarting the entire receipt.

The interface should instruct the Buyer to keep the receipt flat, use good lighting, avoid glare and shadows, keep the text in focus, capture sections from top to bottom, and include a small overlap between consecutive photos.

All images in the set represent one receipt and one audit transaction, not separate receipts.

### Multi-image OCR behavior

OCR should:

1. Process images in the order selected by the Buyer.
2. Combine receipt sections into one extraction result.
3. Detect overlapping text and avoid counting repeated line items twice.
4. Extract one establishment, transaction date, and receipt number.
5. Prefer the final total shown in the bottom receipt section.
6. Flag conflicting dates, totals, or repeated items for Buyer review.
7. Warn when the first or final receipt section appears to be missing.
8. Preserve the draft and other valid images if one image fails.

The Buyer must review and correct the combined OCR result before submission. OCR failure must be reported clearly and must never be replaced with fabricated financial data.

### Image-quality assessment

Each captured or uploaded image should be assessed before OCR for:

- Blur or camera motion
- Insufficient resolution
- Text that is too small
- Darkness or overexposure
- Glare and strong shadows
- Excessive perspective or skew
- Cut-off receipt edges
- Obstructions such as fingers
- A receipt occupying too little of the image

Clearly unusable images should require replacement. Questionable images may show **Retake Photo** and **Use Anyway**, while corrupted or unsupported files must be rejected.

For a long receipt, the warning must identify the affected image and preserve the remaining valid images. For example:

```text
Image 3 of 5 is blurry.
Images 1, 2, 4, and 5 will be preserved.

[Retake Image 3]
```

The camera interface should give actionable guidance such as **Hold steady**, **Move closer**, **More light needed**, **Glare detected**, or **Receipt edge is missing**. Image-quality assessment should happen before sending images to OCR whenever possible.

### Upload safeguards

- Require at least one and allow no more than five images per receipt.
- Validate each image's actual format, decoded content, and file size.
- Support JPEG, PNG, and WEBP.
- Detect accidental duplicate files.
- Preserve image order.
- Show upload and OCR progress.
- Prevent duplicate form submission.

Receipt images should be stored as protected financial documents and served only through authorized access, rather than as unrestricted public static files.

## 2. Petty-Cash Transaction Ledger

Every change to a petty-cash balance should create a permanent transaction record. Recommended transaction types include:

- Opening balance
- Owner-to-manager funding
- Manager-to-buyer funding
- Receipt expense
- Rejected-audit refund
- Cash surrender
- Surrender reversal
- Cash return
- Manual adjustment
- Shortage or overage
- Closing balance

Each transaction should record the amount, direction, transaction type, accounting date, timestamp, affected account, initiating user, approving user when applicable, related audit or surrender reference, notes, and resulting balance.

Confirmed financial records should not be deleted or silently edited. Corrections should use linked reversal or adjustment transactions so the audit trail remains intact.

## 3. Daily Petty-Cash Sessions

Petty cash should use a daily closing and rollover process instead of blindly overwriting balances at midnight.

### Recommended daily calculation

```text
Opening balance
+ additional funding
+ audit refunds
- receipt expenses
- confirmed cash surrendered
= expected remaining cash
```

`DailyStartingFloat` should represent the opening balance for a business day and should not continuously change with the current balance.

### Recommended daily statuses

- Open
- Pending Surrender Confirmation
- Pending Reconciliation
- Closed
- Reopened

Reopening a closed day should require Manager or Owner authorization and a recorded reason.

### Recommended rollover policy

Carry the confirmed closing balance forward as the next day's opening balance. If company policy requires a fixed daily allocation, any difference should be recorded as an explicit replenishment or cash-return transaction rather than silently resetting the balance.

The system should use an explicit company timezone and business-day cutoff. A scheduled process may assist with creating daily sessions or reminders, but it should not erase or overwrite financial history.

## 4. Cash Surrender Requests

Buyers should be able to request a full or partial surrender of their remaining physical cash.

A surrender is a transfer of company cash from the Buyer back to a Manager or Owner. It is not an expense and should be reported separately from purchases.

### Recommended workflow

1. Buyer submits a surrender request.
2. The requested amount becomes reserved but is not permanently deducted.
3. The assigned Manager or an Owner physically receives and counts the cash.
4. The Manager or Owner confirms or rejects the request.
5. Confirmation deducts the amount from the Buyer's balance and records who received it.
6. Rejection or cancellation releases the reserved amount.

### Recommended statuses

- Pending
- Confirmed
- Rejected
- Cancelled

### Authorization rules

- A Manager may confirm requests only from Buyers assigned to that Manager.
- An Owner may confirm any surrender request.
- A Buyer cannot confirm their own request.
- A different Manager cannot confirm a Buyer outside their assigned team.
- A confirmed surrender cannot be edited or deleted; corrections require a reversal.

### Balance behavior

```text
Available balance = recorded balance - pending surrender amount
```

Reserving pending amounts prevents the Buyer from spending or surrendering the same money twice. The permanent balance deduction should occur only after confirmation of physical receipt.

### Discrepancies

If the declared and physically received amounts differ, retain both values and record the variance and reason. Do not silently replace the Buyer's declared amount. The unresolved difference remains part of the Buyer's accountability until resolved.

## 5. Role-Based Sidebar

The sidebar should remain focused on navigation and pending work queues.

### Buyer navigation

- Dashboard
- New Audit
- My Audits
- Cash Surrender
  - New Request
  - My Requests
- Account

### Manager navigation

- Dashboard
- Audit Approvals
- Cash Surrender Requests
- Buyers
- Petty Cash
- Reports
- Account

### Owner navigation

- Dashboard
- Audit Approvals
- Branch Verification
- Cash Surrender Requests
- Users
- Establishments
- Petty Cash
- Reports
- System Settings

Sidebar badges may show the number of pending business records, such as pending audit approvals or cash surrender requests. They should not represent unread personal notifications.

## 6. Header Notification System

Notifications should appear in the application header through a familiar bell icon and dropdown, similar to common social applications. Notifications should not occupy a primary sidebar item.

### Recommended header layout

```text
[Page title]                         [Notification bell] [User menu]
```

The bell should display the unread notification count, using `99+` when appropriate.

### Notification dropdown

The dropdown should show approximately five to ten recent notifications and include:

- Notification category
- Clear title and short message
- Relative or exact timestamp
- Unread indicator
- Direct link to the related record
- **Mark all as read** action
- **View all notifications** link

Opening the dropdown should not automatically mark notifications as read. A notification should become read when the user opens that specific notification or explicitly marks it as read.

The full notification page should support unread and category filters while retaining read notification history.

### Header notifications versus sidebar badges

- **Header bell:** count of unread notifications for the current user.
- **Sidebar badge:** count of business records currently awaiting action.

For example, a Manager may have seven unread notifications but only two cash surrender requests awaiting confirmation.

### Mobile behavior

The bell should remain visible in the mobile header. On small screens, the notification dropdown may open as a full-width panel or bottom sheet.

### Notification integrity rules

- Reading a notification must not approve an audit or confirm a surrender.
- A notification must not grant access to a record the user is not authorized to view.
- Clicking a notification should open the exact related record.
- Notifications should be persisted in the database so a missed popup does not lose the event.
- Repeated reminders should follow an escalation schedule instead of creating excessive duplicates.

## 7. Recommended Notification Events

### Buyer notifications

- Audit submitted successfully
- Audit verified by branch staff
- Audit approved or rejected
- OCR extraction failed and requires manual entry
- Cash surrender confirmed, rejected, or cancelled
- Petty cash added or adjusted
- Available balance is low
- Daily closing is due or incomplete

### Manager notifications

- Assigned Buyer submitted a surrender request
- Audit is awaiting Manager approval
- Buyer reported a reconciliation variance
- Assigned Buyer has not completed daily closing
- Surrender request is nearing or past the cutoff
- Assigned Buyer's balance is below its threshold

### Owner notifications

- Surrender request has exceeded its response time
- Buyer without an assigned Manager submitted a request
- Large surrender or manual adjustment requires attention
- Manager has unresolved daily closings
- Significant cash shortage or overage was reported

Routine surrender requests should notify the assigned Manager first. Owners should retain visibility over all requests but receive direct alerts mainly for unassigned, overdue, high-value, or exceptional cases.

## 8. Reconciliation and Variance Tracking

Daily closing should compare expected cash against cash physically counted.

```text
Expected cash:       PHP 3,800
Actual cash counted: PHP 3,700
Variance:            PHP  -100
```

A non-zero variance should require a structured reason and optional explanation. Possible reasons include:

- Receipt not submitted
- Incorrect expense amount
- Missing cash
- Excess cash
- Pending refund
- Pending surrender
- Data-entry error

A variance should create a reviewable reconciliation record rather than silently changing the balance.

## 9. Structured Rejection Reasons

Audit and surrender rejections should require a reason.

### Audit rejection examples

- Receipt is unreadable
- Wrong establishment
- Amount does not match
- Duplicate receipt
- Receipt is incomplete
- Purchase is unauthorized
- Transaction details are incorrect
- Other

### Surrender rejection examples

- Cash not received
- Amount received does not match
- Duplicate request
- Request submitted to the wrong Manager
- Buyer cancelled the physical handover
- Other

Structured reasons improve reporting while optional notes provide additional context.

## 10. Financial Workflow Safeguards

Recommended protections include:

- Prevent duplicate submissions and double confirmations.
- Permit only the first valid confirmation when a Manager and Owner act concurrently.
- Validate all role and assignment permissions on the server.
- Restrict branch actions to Verify or Reject.
- Restrict Manager approval actions to Approve or Reject.
- Prevent users from approving their own financial requests.
- Require Owner approval for unusually large adjustments when configured thresholds are exceeded.
- Preserve the original expense and create a linked refund when an audit is rejected.

## 11. Reporting and Dashboards

### Buyer dashboard

- Current PCF balance
- Reserved pending-surrender amount
- Available balance
- Today's submitted expenses
- Pending audits
- Pending surrender requests
- Daily closing status

### Manager dashboard

- Assigned Buyers
- Total cash under team accountability
- Audits awaiting approval
- Surrenders awaiting confirmation
- Buyers with low balances
- Incomplete daily closings
- Unresolved variances

### Owner dashboard

- Total distributed petty cash
- Cash currently held by Buyers
- Confirmed surrendered cash
- Approved expenses
- Pending financial actions
- Shortages and overages
- Activity by Manager and establishment

## 12. Google Gemini API Security and OCR Reliability

The currently exposed Gemini API key should be treated as compromised because it has appeared in tracked configuration and application logs. The existing key should be revoked or rotated rather than merely moved.

### Credential handling

1. Disable or delete the exposed key in Google Cloud.
2. Generate separate replacement keys for development and production.
3. Store development credentials in .NET User Secrets or local environment variables.
4. Store production credentials in the hosting platform's encrypted secret facility or a managed secret store.
5. Keep keys out of `appsettings.json`, source code, Razor views, JavaScript, documentation, tests, build artifacts, and version control.
6. Review API usage and securely expire logs that may contain the old key.

The Gemini key must remain server-side and must never be sent to the browser.

### Logging

The application must never log the complete or partial API key or a request URL containing it. Logs may report whether OCR configuration is present without revealing the secret. HTTP and exception logging should redact sensitive query parameters, and raw technical exceptions should not be shown to Buyers.

### Key restrictions and monitoring

- Restrict the key to the required Gemini API.
- Apply supported server or outbound-IP restrictions when the hosting environment permits them reliably.
- Configure quotas, usage monitoring, and spending controls.
- Do not share one unrestricted key between development, testing, production, or unrelated applications.

### Missing or unavailable OCR

Missing configuration or an OCR service outage must not generate sample receipt values. The system should preserve the Buyer's images and draft, explain that automatic extraction is unavailable, and offer:

- Try Again
- Retake or Replace Image
- Enter Details Manually

Mock OCR data should exist only in explicitly configured development or automated-test environments and must never be an automatic production fallback.

### Error-specific Buyer messages

- **Poor image:** explain which image is unreadable and request a retake.
- **Missing section:** request the missing top, middle, or bottom receipt section.
- **Service unavailable:** preserve the draft and offer retry or manual entry.
- **Uncertain extraction:** highlight the fields requiring Buyer review.

### OCR request safeguards

- Enforce the five-image maximum and per-image and total request-size limits.
- Resize unnecessarily large images while preserving readable text.
- Reject malformed or unsupported images before the API request.
- Avoid transmitting duplicate images.
- Use bounded request timeouts and controlled retries.
- Send only receipt images and extraction instructions, not unrelated user, balance, authorization, or credential data.

### OCR response validation

Gemini output must be treated as untrusted input. Validate its structure, field lengths, dates, quantities, prices, totals, and line-item calculations. Highlight ambiguous or conflicting values and require Buyer review before creating an audit record.

The recommended processing sequence is:

```text
Capture or upload images
        ↓
Validate image files
        ↓
Assess image quality
        ↓
Retake unusable images
        ↓
Store images securely
        ↓
Call Gemini from the server
        ↓
Validate the OCR response
        ↓
Buyer reviews highlighted fields
        ↓
Submit the audit
```

The organization should also define a privacy and retention policy for sending receipt information to an external AI provider.

## 13. Additional Recommendations

- Protect receipt images behind authorized access.
- Move API credentials outside tracked configuration and never log them.
- Report OCR failures clearly instead of silently generating sample financial data.
- Detect likely duplicate receipts using Buyer, date, establishment, amount, and image hash.
- Preserve receipt date, upload time, submission time, and verification times separately.
- Add low-balance thresholds and overdue-work escalation rules.
- Generate a printable surrender acknowledgment containing the reference number, parties, amount, and confirmation time.
- Define how pending requests behave when a Buyer is reassigned to a different Manager.
- Add email delivery later for important events; begin with persisted in-application notifications.

## Recommended Delivery Order

### Initial release

1. Secure credentials and receipt storage.
2. Add the petty-cash transaction ledger.
3. Add cash surrender requests with reservation, confirmation, rejection, and cancellation.
4. Add Manager and Owner authorization checks.
5. Add the header notification bell, dropdown, unread count, and full history page.
6. Add role-based sidebar destinations and pending-work badges.
7. Add duplicate-action protection.
8. Integrate surrender requests with daily closing.

### Follow-up release

1. Daily reconciliation and variance tracking.
2. Escalation and low-balance alerts.
3. Reporting and dashboard summaries.
4. Downloadable surrender acknowledgments.
5. Email notification preferences.

### Later, if required

- Browser push notifications
- SMS notifications
- Multi-level approval thresholds
- Advanced duplicate-receipt analysis
- Electronic signatures
