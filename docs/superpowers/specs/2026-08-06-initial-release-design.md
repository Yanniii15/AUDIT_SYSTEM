# Design Specification: Initial Release Workflow Improvements

## 1. Credentials and Receipt Image Security

### Google Gemini API Key
*   Remove the hardcoded API key from `appsettings.json` and `appsettings.Development.json`.
*   Configure the application to load the Gemini API key from environment variables or .NET User Secrets:
    *   Configuration key: `GoogleGemini:ApiKey`
    *   Development storage: `.NET User Secrets` (stored locally outside the repo)
    *   Production storage: Server environment variable `GoogleGemini__ApiKey`
*   Modify `GoogleGeminiOcrService.cs` to remove key printing to logs (`Console.WriteLine($"[GEMINI_OCR] Checking key: '{_apiKey}'")` is replaced with configuration checks only).
*   Add key validation on application startup: if the key is missing or is the default placeholder, log a startup warning but allow the system to boot (OCR will fail gracefully and offer manual entry).

### Receipt Image Access Protection
*   Currently, receipts are saved to `wwwroot/uploads` making them publicly accessible. We will move uploads outside the public web root.
*   Upload target directory: `App_Data/uploads` (or a dedicated directory outside `wwwroot`).
*   Introduce an authorized controller endpoint to serve receipt files:
    *   Route: `/Audits/Receipt/{filename}`
    *   Authorization: Checked server-side. Access is allowed only for:
        *   The Buyer who uploaded the receipt.
        *   The assigned Manager of the Buyer.
        *   Any Owner.
        *   Branch Staff assigned to the same establishment as the receipt.
    *   If authorized, return the file as a stream with the appropriate MIME type. If unauthorized, return `403 Forbidden` or `404 NotFound`.

---

## 2. Multi-Image Upload and OCR Processing

### Database Mapping
*   Currently, `AuditItem` has a single `ReceiptImageUrl` column.
*   We will introduce a new entity `AuditItemImage` to support multiple images per audit:
    ```csharp
    public class AuditItemImage
    {
        [Key]
        public int Id { get; set; }
        public int AuditItemId { get; set; }
        [ForeignKey("AuditItemId")]
        public virtual AuditItem AuditItem { get; set; } = null!;
        [Required]
        [MaxLength(255)]
        public string ImageUrl { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }
    ```
*   `AuditItem` will expose a collection of images:
    ```csharp
    public virtual ICollection<AuditItemImage> Images { get; set; } = new List<AuditItemImage>();
    ```

### Upload View UI & JavaScript
*   Enhance `Views/Audits/Upload.cshtml` to manage up to 5 images.
*   Introduce both file picker (`Choose Images`) and camera trigger (`Take Photo`) buttons.
    *   `<input type="file" id="file-input" multiple accept="image/*">`
    *   `<input type="file" id="camera-input" accept="image/*" capture="environment" class="hidden">`
*   Add a local preview grid containing image cards. Each card will show:
    *   Thumbnail preview
    *   Ordering index (1 to 5)
    *   Actions: Remove, Rotate (90 deg increments using client-side canvas or CSS transforms), and Reorder (Move Up/Down).
*   Form submission builds a `FormData` object containing the files in order, sending them to the controller via AJAX or standard multipart POST.

### Controller & OCR Pipeline
*   `AuditsController.ProcessUpload` accepts a collection of files.
*   Verify file extensions (`.png`, `.jpg`, `.jpeg`, `.webp`), MIME types, and sizes (max 10MB per file).
*   Read each stream, convert to base64, and pass the list of images to the OCR service in their display order.
*   Update `GoogleGeminiOcrService.cs` to map each image as an individual `inlineData` part in the request body.
*   Ensure mock data fallback behavior is removed. If the request fails, return a clear extraction error and redirect to the review page with empty fields, allowing complete manual entry.

---

## 3. Petty-Cash Transaction Ledger

### Database Schema
We will create a `PettyCashLedger` entity to log every credit and debit transaction:
```csharp
public enum LedgerTransactionType
{
    VaultFunding,      // Owner funding themselves / Master Vault
    ManagerFunding,    // Manager/Owner funding a Buyer
    ExpenseDeduction,  // Buyer submitting an expense
    ReversalRefund,    // Refund from rejected expense
    CashSurrender,     // Buyer returning cash to Manager/Owner
    ManualAdjustment   // Manual corrections by Owner
}

public class PettyCashLedger
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; } // Affected user balance
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [Required]
    public LedgerTransactionType TransactionType { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Amount { get; set; } // Positive for credits, negative for debits

    [Column(TypeName = "decimal(12,2)")]
    public decimal ResultingBalance { get; set; }

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public int? AssociatedRecordId { get; set; } // AuditItem ID or SurrenderRequest ID

    public int? CounterpartyUserId { get; set; } // Manager who funded or confirmed
    [ForeignKey("CounterpartyUserId")]
    public virtual User? CounterpartyUser { get; set; }

    [MaxLength(255)]
    public string? Notes { get; set; }
}
```

### Ledger Updates
*   Every balance-altering action must write to this ledger within a database transaction.
*   Actions affected:
    1.  `UsersController.AddPcf` (Vault funding, transfers between supervisor and buyer).
    2.  `AuditsController.SubmitAudit` (Expense deduction from buyer's PCF).
    3.  `AuditsController.Verify` / `BranchVerify` (Reversal refunds upon audit rejection).
    4.  Cash Surrender confirmations (see Section 4).

---

## 4. Cash Surrender Workflow

### Database Schema
We will create a `SurrenderRequest` entity:
```csharp
public enum SurrenderStatus
{
    Pending,
    Confirmed,
    Rejected,
    Cancelled
}

public class SurrenderRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int BuyerId { get; set; }
    [ForeignKey("BuyerId")]
    public virtual User Buyer { get; set; } = null!;

    [Required]
    [Column(TypeName = "decimal(12,2)")]
    public decimal DeclaredAmount { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal? ConfirmedAmount { get; set; }

    [Required]
    public SurrenderStatus Status { get; set; } = SurrenderStatus.Pending;

    [Required]
    public DateTime RequestDate { get; set; } = DateTime.Now;

    public DateTime? ActionDate { get; set; }

    public int? ActionByUserId { get; set; }
    [ForeignKey("ActionByUserId")]
    public virtual User? ActionByUser { get; set; }

    [MaxLength(255)]
    public string? BuyerNotes { get; set; }

    [MaxLength(255)]
    public string? ActionNotes { get; set; }
}
```

### Business Logic & Available Balances
*   Introduce the concept of "Available Balance" in calculation paths.
    *   `AvailableBalance = PcfBalance - Sum(Pending Surrenders)`
*   When a Buyer requests a surrender, verify:
    *   `DeclaredAmount > 0`
    *   `DeclaredAmount <= AvailableBalance`
*   Upon submission, the amount is reserved (reducing their available balance for new expenses or surrenders), but the physical `PcfBalance` is not yet deducted.
*   Confirmation:
    *   Executed by the assigned Manager or an Owner.
    *   The confirmed amount is deducted from the Buyer's `PcfBalance` and `DailyStartingFloat`.
    *   A ledger entry of type `CashSurrender` is written.
*   Rejection / Cancellation:
    *   Status is updated.
    *   The reserved amount is released back to the Buyer's available pool.

---

## 5. Supervisor Verification Limits

Enforce strict server-side rules in controllers:
*   **Audit Approvals (`AuditsController.Verify`):**
    *   If the current user is a `Manager`, check that the audit buyer's `ManagerId` matches the current manager's ID. If not, return `403 Forbidden`.
*   **Cash Transfers (`UsersController.AddPcf`):**
    *   If the current user is a `Manager`, check that the target user's `ManagerId` is equal to the current manager's ID. If not, return `403 Forbidden`.
*   **Surrender Requests:**
    *   A manager can only list and confirm surrender requests from buyers assigned to them.
*   **Owners:** Owners bypass these supervisor checks and can manage all transfers, approvals, and surrenders.

---

## 6. Role-Based Sidebar Navigation

Update `Views/Shared/_Layout.cshtml` sidebar elements:
*   Use `User.IsInRole(...)` to render custom navigation panels:
    *   **Buyer:** Dashboard, New Audit, My Audits, Cash Surrender (Form & Requests).
    *   **Manager:** Dashboard, Audit Approvals, Cash Surrender Requests, Buyers, Petty Cash, Reports.
    *   **Owner:** Dashboard, Audit Approvals, Branch Verification, Cash Surrender Requests, Users, Establishments, Petty Cash, Reports, System Settings.
*   Add dynamic badge elements querying pending records in layouts:
    *   `Audit Approvals` badge shows count of items with status `AwaitingManagerApproval` (filtered by supervisor assignment for managers).
    *   `Cash Surrender Requests` badge shows count of `Pending` requests.

---

## 7. Header Notification System

### Database Schema
Create a `Notification` entity:
```csharp
public class Notification
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Message { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty; // e.g. Audit, Surrender, Cash

    [MaxLength(255)]
    public string? LinkUrl { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? ReadAt { get; set; }
}
```

### Layout Integration
*   Add a bell icon and unread count badge to the header.
*   Clicking the bell opens a dropdown containing the 5 most recent notifications.
*   Add an AJAX endpoint `/Notifications/MarkAsRead` that accepts a notification ID and sets `ReadAt = DateTime.Now` (or marks all as read).
*   Add a dedicated view `/Notifications/Index` showing all historical notifications.

---

## 8. Double-Action Protection

*   **Client-Side:**
    *   Disable submit and approval buttons once clicked.
    *   Display a spinner or loading text.
*   **Server-Side:**
    *   Introduce an idempotency key or verify the entity's status before mutating.
    *   In `AuditsController.Verify`, check:
        ```csharp
        if (audit.Status != AuditStatus.AwaitingManagerApproval)
        {
            return BadRequest("This audit item has already been processed.");
        }
        ```
    *   In `BranchVerify`, verify status is exactly `AwaitingBranchVerification`.
    *   In `SurrenderRequest` confirmation, verify status is exactly `Pending`.
