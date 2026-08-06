# Initial Release Workflow Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement credentials rotation, secure receipt access, multi-image intake forms with canvas rotation, a database transaction ledger, supervisor verification checks, role-based sidebar menus, a header notification system, and double-action submit protection.

**Architecture:** Database updates will introduce `AuditItemImage`, `PettyCashLedger`, `SurrenderRequest`, and `Notification` models. MVC controllers and actions are guarded with role and supervisor ownership limits. Frontend layouts, views, and JavaScript inputs are updated for multi-image rendering, file pickers, cameras, and notification counts.

**Tech Stack:** .NET 9, EF Core, MySQL (Pomelo), BCrypt, jQuery, HTML5, CSS Tailwind.

---

### Task 1: Google Gemini API Security and OCR Error Fallback

**Files:**
- Modify: `AuditCkDayo/appsettings.json`
- Modify: `AuditCkDayo/Services/GoogleGeminiOcrService.cs`
- Modify: `AuditCkDayo/Controllers/AuditsController.cs`

- [ ] **Step 1: Rotate and Remove API Key from settings**
  Edit `appsettings.json` and replace the GoogleGemini:ApiKey value with an empty string:
  ```json
  "GoogleGemini": {
    "ApiKey": ""
  }
  ```

- [ ] **Step 2: Update OCR Service to check User Secrets and Environment Variables**
  Modify `GoogleGeminiOcrService.cs` constructor to fetch key without logs:
  ```csharp
  public GoogleGeminiOcrService(IConfiguration configuration)
  {
      _apiKey = configuration["GoogleGemini:ApiKey"] ?? "";
      _httpClient = new HttpClient();
  }
  ```
  Ensure the line `Console.WriteLine($"[GEMINI_OCR] Checking key: '{_apiKey}'");` is removed.

- [ ] **Step 3: Modify OCR ParseReceiptAsync to fail when API Key is missing**
  Remove mock data fallbacks in `ParseReceiptAsync`. If the key is empty or request fails, throw an exception:
  ```csharp
  if (string.IsNullOrEmpty(_apiKey))
  {
      throw new InvalidOperationException("Gemini API key is not configured.");
  }
  ```

- [ ] **Step 4: Update AuditsController.ProcessUpload Exception Handling**
  Modify `ProcessUpload` in `AuditsController.cs` to redirect to `Review` with empty VM values on OCR failure:
  ```csharp
  try
  {
      using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
      {
          var ocrResult = await _ocrService.ParseReceiptAsync(stream);
          HttpContext.Session.SetString("TotalAmount", ocrResult.TotalAmount.ToString("F2"));
          HttpContext.Session.SetString("TransactionDate", ocrResult.TransactionDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd"));
          HttpContext.Session.SetString("OcrItems", System.Text.Json.JsonSerializer.Serialize(ocrResult.Items));
      }
  }
  catch (Exception ex)
  {
      // Log exception securely and set empty values to allow manual entry
      HttpContext.Session.SetString("TotalAmount", "0.00");
      HttpContext.Session.SetString("TransactionDate", DateTime.Today.ToString("yyyy-MM-dd"));
      HttpContext.Session.SetString("OcrItems", "[]");
      TempData["Warning"] = "OCR scan failed. Please enter the details manually.";
  }
  ```

- [ ] **Step 5: Run tests and verify project builds**
  Run: `dotnet build`
  Expected: Successful compilation with 0 errors.

---

### Task 2: Secure Receipt Image Storage and Route Authorization

**Files:**
- Modify: `AuditCkDayo/Controllers/AuditsController.cs`
- Modify: `AuditCkDayo/Program.cs`

- [ ] **Step 1: Move Upload Target Outside public web root**
  Update `AuditsController.ProcessUpload` file destination from `wwwroot/uploads` to `App_Data/uploads`:
  ```csharp
  var baseDirectory = AppContext.BaseDirectory;
  var uploadsFolder = Path.Combine(baseDirectory, "App_Data", "uploads");
  ```

- [ ] **Step 2: Implement File Download Endpoint with Role check**
  Add a `Receipt` action to `AuditsController.cs` with ownership restrictions:
  ```csharp
  [HttpGet]
  [Authorize]
  public async Task<IActionResult> Receipt(string filename)
  {
      var baseDirectory = AppContext.BaseDirectory;
      var filePath = Path.Combine(baseDirectory, "App_Data", "uploads", filename);
      if (!System.IO.File.Exists(filePath))
      {
          return NotFound();
      }

      var currentUserIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
      var role = User.FindFirstValue(ClaimTypes.Role);
      if (!int.TryParse(currentUserIdString, out var currentUserId))
      {
          return Challenge();
      }

      // Read audit item details from database to check permissions
      var audit = await _context.AuditItems.Include(a => a.Buyer).FirstOrDefaultAsync(a => a.ReceiptImageUrl.Contains(filename));
      if (audit == null)
      {
          return NotFound();
      }

      bool isAuthorized = role == "Owner" || 
                           audit.BuyerId == currentUserId || 
                           (role == "Manager" && audit.Buyer.ManagerId == currentUserId);

      if (!isAuthorized)
      {
          return Forbid();
      }

      var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
      return File(fileBytes, "image/jpeg");
  }
  ```

- [ ] **Step 3: Run build to verify changes**
  Run: `dotnet build`
  Expected: Build succeeds.

---

### Task 3: Multi-Image Database Schema and Intake Form

**Files:**
- Create: `AuditCkDayo/Models/AuditItemImage.cs`
- Modify: `AuditCkDayo/Models/AuditItem.cs`
- Modify: `AuditCkDayo/Data/AuditDbContext.cs`

- [ ] **Step 1: Create AuditItemImage Entity model**
  Write to `AuditCkDayo/Models/AuditItemImage.cs`:
  ```csharp
  using System.ComponentModel.DataAnnotations;
  using System.ComponentModel.DataAnnotations.Schema;

  namespace AuditCkDayo.Models
  {
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
  }
  ```

- [ ] **Step 2: Add DbSet mapping to DbContext**
  Modify `AuditDbContext.cs` to include the relationship and DbSet mapping:
  ```csharp
  public DbSet<AuditItemImage> AuditItemImages { get; set; }
  ```
  In `OnModelCreating`, add:
  ```csharp
  modelBuilder.Entity<AuditItemImage>()
      .HasOne(ai => ai.AuditItem)
      .WithMany(a => a.Images)
      .HasForeignKey(ai => ai.AuditItemId)
      .OnDelete(DeleteBehavior.Cascade);
  ```

- [ ] **Step 3: Update Upload Form view**
  Modify `Views/Audits/Upload.cshtml` JavaScript to collect multiple images locally, and use a hidden collection of files when posting `ProcessUpload`.

- [ ] **Step 4: Update IOcrService signature**
  Modify `IOcrService.cs` and `GoogleGeminiOcrService.cs` to parse a list of images in batch:
  ```csharp
  Task<OcrResult> ParseReceiptAsync(List<Stream> imageStreams);
  ```

- [ ] **Step 5: Run EF Migrations**
  Run: `dotnet ef migrations add AddMultiImageAudits`
  Expected: Successful generation of schema migration files.

---

### Task 4: Transaction Ledger Implementation

**Files:**
- Create: `AuditCkDayo/Models/PettyCashLedger.cs`
- Modify: `AuditCkDayo/Data/AuditDbContext.cs`
- Modify: `AuditCkDayo/Controllers/UsersController.cs`
- Modify: `AuditCkDayo/Controllers/AuditsController.cs`

- [ ] **Step 1: Create PettyCashLedger Model**
  Write model attributes and transaction types matching Section 3 of design spec to `AuditCkDayo/Models/PettyCashLedger.cs`.

- [ ] **Step 2: Track balance updates in UsersController.AddPcf**
  Wrap PCF transfers in a database transaction, updating User properties and creating a ledger row:
  ```csharp
  var ledger = new PettyCashLedger
  {
      UserId = targetUser.Id,
      TransactionType = isSelfTransfer ? LedgerTransactionType.VaultFunding : LedgerTransactionType.ManagerFunding,
      Amount = finalAmount,
      ResultingBalance = targetUser.PcfBalance,
      Timestamp = DateTime.Now,
      CounterpartyUserId = currentUser.Id
  };
  _context.PettyCashLedgers.Add(ledger);
  ```

- [ ] **Step 3: Track audit submissions and reversals**
  Upon expense submissions in `SubmitAudit` and refunds in `Verify` / `BranchVerify`, add equivalent `PettyCashLedger` rows before committing context adjustments.

- [ ] **Step 4: Add EF Migrations**
  Run: `dotnet ef migrations add AddLedger`
  Expected: Migration generated. Run `dotnet ef database update`.

---

### Task 5: Cash Surrender Flow and Available Balance Reservations

**Files:**
- Create: `AuditCkDayo/Models/SurrenderRequest.cs`
- Modify: `AuditCkDayo/Data/AuditDbContext.cs`
- Modify: `AuditCkDayo/Controllers/AuditsController.cs`

- [ ] **Step 1: Create SurrenderRequest Model**
  Write model mappings and statuses matching Section 4 of design spec to `AuditCkDayo/Models/SurrenderRequest.cs`.

- [ ] **Step 2: Add Available Balance logic**
  Implement getter properties and check rules during new requests:
  ```csharp
  decimal pendingAmount = await _context.SurrenderRequests
      .Where(s => s.BuyerId == buyerId && s.Status == SurrenderStatus.Pending)
      .SumAsync(s => s.DeclaredAmount);
  decimal availableBalance = buyer.PcfBalance - pendingAmount;
  if (model.Amount > availableBalance) {
      ModelState.AddModelError("", "Insufficient available funds due to pending surrenders.");
  }
  ```

- [ ] **Step 3: Implement Confirmation/Rejection operations**
  Confirming surrenders updates `PcfBalance` and `DailyStartingFloat`, changes request status to `Confirmed`, and creates a ledger debit row. Rejections change status to `Rejected` releasing the reservation.

- [ ] **Step 4: Execute Schema Update**
  Run: `dotnet ef migrations add AddCashSurrenders && dotnet ef database update`
  Expected: Success.

---

### Task 6: Supervisor Verification Restrictions

**Files:**
- Modify: `AuditCkDayo/Controllers/UsersController.cs`
- Modify: `AuditCkDayo/Controllers/AuditsController.cs`

- [ ] **Step 1: Restrict balance edits in UsersController**
  Ensure managers can only target users with a matching `ManagerId`:
  ```csharp
  if (!User.IsInRole("Owner") && targetUser.ManagerId != currentUserId)
  {
      return Forbid();
  }
  ```

- [ ] **Step 2: Restrict manager verification and lists**
  In `AuditsController.VerifyList` and `Verify`, return `Forbid()` if the current manager's ID does not match `buyer.ManagerId`.

- [ ] **Step 3: Run unit tests to confirm transfer permissions**
  Run: `dotnet test`
  Expected: 12 passing tests.

---

### Task 7: Layout Navigation and Badges

**Files:**
- Modify: `AuditCkDayo/Views/Shared/_Layout.cshtml`

- [ ] **Step 1: Update Sidebar Views**
  Structure HTML sidebar routes based on role layout queries using `User.IsInRole("Owner")`, `User.IsInRole("Manager")`, and `User.IsInRole("Buyer")`.

- [ ] **Step 2: Dynamic Sidebar Badges**
  Query pending values asynchronously and display them adjacent to navigation anchors.

---

### Task 8: Header Notification Bell and Dropdown

**Files:**
- Create: `AuditCkDayo/Models/Notification.cs`
- Create: `AuditCkDayo/Controllers/NotificationsController.cs`
- Modify: `AuditCkDayo/Views/Shared/_Layout.cshtml`

- [ ] **Step 1: Create Notification Model**
  Write model mapping fields matching Section 7 of design spec to `AuditCkDayo/Models/Notification.cs`.

- [ ] **Step 2: Add layout header layout code**
  Embed a bell icon, unread counter badge, and absolute dropdown container to `_Layout.cshtml` header.

- [ ] **Step 3: Implement Actions in NotificationsController**
  Expose JSON API endpoints to fetch unread items, clear alert badges, and render the complete notification index view.

- [ ] **Step 4: Execute database migration**
  Run: `dotnet ef migrations add AddNotifications && dotnet ef database update`
  Expected: Solution compiles and databases migrate successfully.
