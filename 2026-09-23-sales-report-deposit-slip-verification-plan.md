# Sales Report Bank Deposit Slip Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow managers to upload bank deposit slips for each establishment's daily cash sales with Tesseract OCR auto-fill, strictly record 1:1 deposit matching/variance, and display a prominent red warning in the Treasury until the deposit slip proof is provided.

**Architecture:** Extend `SalesReport` with deposit slip metadata (image URL, amount, bank name, reference number, date, variance reason) and link `CashFlowEntry` directly to `SalesReport`. Provide a Tesseract OCR pre-fill endpoint (`ExtractDepositSlipOcr`), an upload endpoint (`UploadDepositSlip`), and a secure image retrieval route (`DepositSlip`). Update `/Treasury` to highlight un-deposited sales inflows in red with a `NO DEPOSIT SLIP` warning badge, and update `/SalesReports` with a quick-upload modal and review card.

**Tech Stack:** ASP.NET Core 9 MVC, EF Core 9, Pomelo MySQL, Tesseract 5.2.0, SixLabors.ImageSharp, Tailwind CSS, xUnit with SQLite in-memory test suite.

---

## File Structure & Responsibilities

- **Modify:** `AuditCkDayo/Program.cs` — Fix syntax slip on line 166 and add startup migration check for deposit slip columns.
- **Modify:** `AuditCkDayo/Models/SalesReport.cs` — Add deposit slip fields and computed properties (`HasDepositSlip`, `DepositVariance`, `IsDepositMatched`).
- **Modify:** `AuditCkDayo/Models/CashFlowEntry.cs` — Add optional `SalesReportId` foreign key and navigation property.
- **Modify:** `AuditCkDayo/Data/AuditDbContext.cs` — Configure precision, column lengths, and relationships for the new fields.
- **Create:** `AuditCkDayo/Services/IDepositSlipOcrService.cs` — Contract for deposit slip OCR extraction.
- **Modify:** `AuditCkDayo/Services/TesseractOcrService.cs` — Implement `ParseDepositSlipAsync` detecting Bank, Amount, Reference #, and Date using regex.
- **Modify:** `AuditCkDayo/ViewModels/SalesReportReviewViewModel.cs` — Add deposit slip properties and view models for modal upload.
- **Modify:** `AuditCkDayo/Controllers/SalesReportsController.cs` — Add `ExtractDepositSlipOcr`, `UploadDepositSlip`, `DepositSlip/{fileName}` actions, and link `CashFlowEntry.SalesReportId` on sales confirmation.
- **Modify:** `AuditCkDayo/Views/SalesReports/Index.cshtml` — Add "Deposit Slip" status column and Quick-Upload Modal.
- **Modify:** `AuditCkDayo/Views/SalesReports/ReviewManager.cshtml` — Add "Bank Deposit Verification" card with live variance calculator.
- **Modify:** `AuditCkDayo/Views/Treasury/Index.cshtml` — Highlight sales inflows lacking deposit slips in red with `NO DEPOSIT SLIP` badge and top warning banner.
- **Create:** `AuditCkDayo.Tests/SalesReportDepositSlipTests.cs` — xUnit tests covering model behavior, OCR extraction regex, Treasury red status logic, and variance validation.

---

### Task 1: Fix Program.cs Compiler Blocker & Add Model Fields to SalesReport and CashFlowEntry

**Files:**
- Modify: `AuditCkDayo/Program.cs:1-10, 160-166`
- Modify: `AuditCkDayo/Models/SalesReport.cs:30-70`
- Modify: `AuditCkDayo/Models/CashFlowEntry.cs:60-70`
- Create: `AuditCkDayo.Tests/SalesReportDepositSlipTests.cs`

- [ ] **Step 1: Write the failing test**

Create `AuditCkDayo.Tests/SalesReportDepositSlipTests.cs`:

```csharp
using System;
using AuditCkDayo.Models;
using Xunit;

namespace AuditCkDayo.Tests
{
    public class SalesReportDepositSlipTests
    {
        [Fact]
        public void SalesReport_HasDepositSlipProperties_AndComputesVarianceCorrectly()
        {
            var report = new SalesReport
            {
                ConfirmedCashToHandover = 50000.00m,
                DepositedAmount = 50000.00m,
                DepositSlipImageUrl = "/SalesReports/DepositSlip/test.jpg",
                DepositBankName = "BDO",
                DepositReferenceNumber = "TRN-12345",
                DepositDate = DateTime.Today
            };

            Assert.True(report.HasDepositSlip);
            Assert.Equal(0.00m, report.DepositVariance);
            Assert.True(report.IsDepositMatched);
            Assert.False(report.IsDepositDiscrepancy);

            // Test Short Variance
            report.DepositedAmount = 49500.00m;
            Assert.Equal(-500.00m, report.DepositVariance);
            Assert.False(report.IsDepositMatched);
            Assert.True(report.IsDepositDiscrepancy);

            // Test Over Variance
            report.DepositedAmount = 50200.00m;
            Assert.Equal(200.00m, report.DepositVariance);
            Assert.False(report.IsDepositMatched);
            Assert.True(report.IsDepositDiscrepancy);
        }

        [Fact]
        public void CashFlowEntry_HasSalesReportRelationshipProperties()
        {
            var property = typeof(CashFlowEntry).GetProperty("SalesReportId");
            Assert.NotNull(property);

            var navProperty = typeof(CashFlowEntry).GetProperty("SalesReport");
            Assert.NotNull(navProperty);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter SalesReportDepositSlipTests
```
Expected: Compilation failure or missing property errors.

- [ ] **Step 3: Fix `Program.cs` and add properties to models**

1. In `AuditCkDayo/Program.cs`:
   Remove line 4 `using System.Runtime.Intrinsics.Arm;` and remove line 166 `AdvSimd asdasds`.
2. In `AuditCkDayo/Models/SalesReport.cs`, inside `public class SalesReport`:
```csharp
        // --- Bank Deposit Slip Verification Fields ---
        [MaxLength(255)]
        public string? DepositSlipImageUrl { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? DepositedAmount { get; set; }

        [MaxLength(100)]
        public string? DepositBankName { get; set; }

        [MaxLength(100)]
        public string? DepositReferenceNumber { get; set; }

        public DateTime? DepositDate { get; set; }

        [MaxLength(500)]
        public string? DepositVarianceReason { get; set; }

        public int? DepositUploadedByUserId { get; set; }

        [ForeignKey("DepositUploadedByUserId")]
        public virtual User? DepositUploadedByUser { get; set; }

        public DateTime? DepositUploadedAt { get; set; }

        [NotMapped]
        public bool HasDepositSlip => !string.IsNullOrWhiteSpace(DepositSlipImageUrl);

        [NotMapped]
        public decimal DepositVariance => (DepositedAmount ?? 0m) - ConfirmedCashToHandover;

        [NotMapped]
        public bool IsDepositMatched => HasDepositSlip && Math.Abs(DepositVariance) < 0.01m;

        [NotMapped]
        public bool IsDepositDiscrepancy => HasDepositSlip && Math.Abs(DepositVariance) >= 0.01m;
```
3. In `AuditCkDayo/Models/CashFlowEntry.cs`:
```csharp
        public int? SalesReportId { get; set; }

        [ForeignKey("SalesReportId")]
        public virtual SalesReport? SalesReport { get; set; }
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter SalesReportDepositSlipTests
```
Expected: PASS (2 tests passed).

- [ ] **Step 5: Commit**

```bash
git add AuditCkDayo/Program.cs AuditCkDayo/Models/SalesReport.cs AuditCkDayo/Models/CashFlowEntry.cs AuditCkDayo.Tests/SalesReportDepositSlipTests.cs
git commit -m "feat(model): add deposit slip fields to SalesReport and link CashFlowEntry"
```

---

### Task 2: Database Context Configuration & Startup Migrations

**Files:**
- Modify: `AuditCkDayo/Data/AuditDbContext.cs`
- Modify: `AuditCkDayo/Program.cs`

- [ ] **Step 1: Write test verifying AuditDbContext entity configuration**

Add to `AuditCkDayo.Tests/SalesReportDepositSlipTests.cs`:

```csharp
        [Fact]
        public void AuditDbContext_CanPersistSalesReportDepositSlipFields()
        {
            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AuditCkDayo.Data.AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditCkDayo.Data.AuditDbContext(options);
            var report = new SalesReport
            {
                BusinessDate = DateTime.Today,
                HandoverDate = DateTime.Today,
                DepositSlipImageUrl = "/SalesReports/DepositSlip/test_receipt.jpg",
                DepositedAmount = 12500.50m,
                DepositBankName = "BPI",
                DepositReferenceNumber = "REF-9988",
                DepositDate = DateTime.Today,
                DepositVarianceReason = "Matched"
            };

            context.SalesReports.Add(report);
            context.SaveChanges();

            var loaded = context.SalesReports.Find(report.Id);
            Assert.NotNull(loaded);
            Assert.Equal("BPI", loaded.DepositBankName);
            Assert.Equal(12500.50m, loaded.DepositedAmount);
            Assert.True(loaded.HasDepositSlip);
        }
```

- [ ] **Step 2: Run test to verify failure or compile status**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter AuditDbContext_CanPersistSalesReportDepositSlipFields
```

- [ ] **Step 3: Update `AuditDbContext.cs` and `Program.cs` startup migrations**

In `AuditCkDayo/Data/AuditDbContext.cs`, in `OnModelCreating`:
```csharp
            modelBuilder.Entity<SalesReport>()
                .Property(s => s.DepositedAmount)
                .HasPrecision(12, 2);

            modelBuilder.Entity<CashFlowEntry>()
                .HasOne(e => e.SalesReport)
                .WithMany()
                .HasForeignKey(e => e.SalesReportId)
                .OnDelete(DeleteBehavior.SetNull);
```

In `AuditCkDayo/Program.cs`, in the startup migration scope:
```csharp
    try
    {
        db.Database.ExecuteSqlRaw(@"
            ALTER TABLE SalesReports 
                ADD COLUMN IF NOT EXISTS DepositSlipImageUrl varchar(255) NULL,
                ADD COLUMN IF NOT EXISTS DepositedAmount decimal(12,2) NULL,
                ADD COLUMN IF NOT EXISTS DepositBankName varchar(100) NULL,
                ADD COLUMN IF NOT EXISTS DepositReferenceNumber varchar(100) NULL,
                ADD COLUMN IF NOT EXISTS DepositDate datetime NULL,
                ADD COLUMN IF NOT EXISTS DepositVarianceReason varchar(500) NULL,
                ADD COLUMN IF NOT EXISTS DepositUploadedByUserId int NULL,
                ADD COLUMN IF NOT EXISTS DepositUploadedAt datetime NULL;

            ALTER TABLE CashFlowEntries
                ADD COLUMN IF NOT EXISTS SalesReportId int NULL;
        ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB_INIT] Deposit slip schema update notice: {ex.Message}");
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter AuditDbContext_CanPersistSalesReportDepositSlipFields
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add AuditCkDayo/Data/AuditDbContext.cs AuditCkDayo/Program.cs AuditCkDayo.Tests/SalesReportDepositSlipTests.cs
git commit -m "feat(db): configure SalesReport deposit slip EF mappings and startup migrations"
```

---

### Task 3: Tesseract OCR Engine Extraction for Deposit Slips

**Files:**
- Create: `AuditCkDayo/Services/IDepositSlipOcrService.cs`
- Modify: `AuditCkDayo/Services/TesseractOcrService.cs`
- Modify: `AuditCkDayo.Tests/SalesReportDepositSlipTests.cs`

- [ ] **Step 1: Write unit tests for Deposit Slip OCR pattern parsing**

Add to `AuditCkDayo.Tests/SalesReportDepositSlipTests.cs`:

```csharp
        [Theory]
        [InlineData("BDO UNIBANK CASH DEPOSIT TRN: 948201 AMOUNT: PHP 45,780.00 DATE: 2026-08-15", "BDO", 45780.00, "948201")]
        [InlineData("BANK OF THE PHILIPPINE ISLANDS REF# 88219 TOTAL CASH: 12,500.50 08/16/2026", "BPI", 12500.50, "88219")]
        [InlineData("METROBANK CASH DEPOSIT P35,000.00 TRACE 77123", "Metrobank", 35000.00, "77123")]
        public void DepositSlipOcrService_ExtractsFieldsFromRawTextCorrectly(string rawText, string expectedBank, decimal expectedAmount, string expectedRef)
        {
            var result = AuditCkDayo.Services.TesseractOcrService.ParseDepositSlipText(rawText);

            Assert.Equal(expectedBank, result.DetectedBank);
            Assert.Equal(expectedAmount, result.DetectedAmount);
            Assert.Contains(expectedRef, result.DetectedReference);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter DepositSlipOcrService_ExtractsFieldsFromRawTextCorrectly
```
Expected: Method does not exist.

- [ ] **Step 3: Implement `ParseDepositSlipText` and `ParseDepositSlipAsync` in `TesseractOcrService.cs`**

Create `AuditCkDayo/Services/IDepositSlipOcrService.cs`:
```csharp
using System.IO;
using System.Threading.Tasks;

namespace AuditCkDayo.Services
{
    public class DepositSlipOcrResult
    {
        public bool Success { get; set; }
        public string? DetectedBank { get; set; }
        public decimal? DetectedAmount { get; set; }
        public string? DetectedReference { get; set; }
        public DateTime? DetectedDate { get; set; }
        public string? RawText { get; set; }
    }

    public interface IDepositSlipOcrService
    {
        Task<DepositSlipOcrResult> ParseDepositSlipAsync(Stream imageStream);
    }
}
```

In `AuditCkDayo/Services/TesseractOcrService.cs`, implement `IDepositSlipOcrService` and add:
```csharp
        public static DepositSlipOcrResult ParseDepositSlipText(string text)
        {
            var result = new DepositSlipOcrResult { RawText = text };
            if (string.IsNullOrWhiteSpace(text)) return result;

            // Bank Detection
            if (Regex.IsMatch(text, @"\b(BDO|BANCO DE ORO)\b", RegexOptions.IgnoreCase)) result.DetectedBank = "BDO";
            else if (Regex.IsMatch(text, @"\b(BPI|BANK OF THE PHILIPPINE ISLANDS)\b", RegexOptions.IgnoreCase)) result.DetectedBank = "BPI";
            else if (Regex.IsMatch(text, @"\b(METROBANK|METROPOLITAN)\b", RegexOptions.IgnoreCase)) result.DetectedBank = "Metrobank";
            else if (Regex.IsMatch(text, @"\b(LANDBANK|LAND BANK)\b", RegexOptions.IgnoreCase)) result.DetectedBank = "Landbank";
            else if (Regex.IsMatch(text, @"\b(SECURITY BANK)\b", RegexOptions.IgnoreCase)) result.DetectedBank = "Security Bank";
            else if (Regex.IsMatch(text, @"\b(CHINA BANK)\b", RegexOptions.IgnoreCase)) result.DetectedBank = "China Bank";
            else if (Regex.IsMatch(text, @"\b(UNIONBANK|UNION BANK)\b", RegexOptions.IgnoreCase)) result.DetectedBank = "UnionBank";
            else if (Regex.IsMatch(text, @"\b(PNB|PHILIPPINE NATIONAL BANK)\b", RegexOptions.IgnoreCase)) result.DetectedBank = "PNB";

            // Reference Number Detection
            var refMatch = Regex.Match(text, @"(?:TRN|REF|TRACE|TRANS|BATCH)[\s#:.-]*([A-Z0-9-]{4,20})", RegexOptions.IgnoreCase);
            if (refMatch.Success) result.DetectedReference = refMatch.Groups[1].Value.Trim();

            // Amount Detection
            var amountMatches = Regex.Matches(text, @"(?:PHP|P|₱|TOTAL|AMOUNT|CASH)[\s:]*([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{2})?)", RegexOptions.IgnoreCase);
            foreach (Match match in amountMatches)
            {
                var clean = match.Groups[1].Value.Replace(",", "");
                if (decimal.TryParse(clean, out var val) && val > 0)
                {
                    result.DetectedAmount = val;
                    break;
                }
            }

            // Fallback plain amount pattern if not preceded by currency label
            if (!result.DetectedAmount.HasValue)
            {
                var plainAmountMatch = Regex.Match(text, @"\b([1-9][0-9]{1,2}(?:,[0-9]{3})*\.[0-9]{2})\b");
                if (plainAmountMatch.Success && decimal.TryParse(plainAmountMatch.Groups[1].Value.Replace(",", ""), out var plainVal))
                {
                    result.DetectedAmount = plainVal;
                }
            }

            // Date Detection
            var dateMatch = Regex.Match(text, @"\b(202[0-9][-/.](?:0[1-9]|1[0-2])[-/.](?:0[1-9]|[12][0-9]|3[01]))\b");
            if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var parsedDate))
            {
                result.DetectedDate = parsedDate;
            }
            else
            {
                var usDateMatch = Regex.Match(text, @"\b((?:0[1-9]|1[0-2])[-/.](?:0[1-9]|[12][0-9]|3[01])[-/.](?:202[0-9]|2[0-9]))\b");
                if (usDateMatch.Success && DateTime.TryParse(usDateMatch.Groups[1].Value, out var parsedUsDate))
                {
                    result.DetectedDate = parsedUsDate;
                }
            }

            result.Success = result.DetectedAmount.HasValue || !string.IsNullOrEmpty(result.DetectedBank);
            return result;
        }

        public async Task<DepositSlipOcrResult> ParseDepositSlipAsync(Stream imageStream)
        {
            try
            {
                using var ms = new MemoryStream();
                await imageStream.CopyToAsync(ms);
                var bytes = ms.ToArray();
                var text = ProcessTesseract(bytes);
                return ParseDepositSlipText(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TESSERACT_DEPOSIT_OCR] Error: {ex.Message}");
                return new DepositSlipOcrResult { Success = false };
            }
        }
```

- [ ] **Step 4: Register service in `Program.cs` and run test to verify it passes**

In `AuditCkDayo/Program.cs`:
```csharp
builder.Services.AddScoped<AuditCkDayo.Services.IDepositSlipOcrService, AuditCkDayo.Services.TesseractOcrService>();
```

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter DepositSlipOcrService_ExtractsFieldsFromRawTextCorrectly
```
Expected: PASS (3 tests passed).

- [ ] **Step 5: Commit**

```bash
git add AuditCkDayo/Services/IDepositSlipOcrService.cs AuditCkDayo/Services/TesseractOcrService.cs AuditCkDayo/Program.cs AuditCkDayo.Tests/SalesReportDepositSlipTests.cs
git commit -m "feat(ocr): add Tesseract deposit slip parser and unit tests"
```

---

### Task 4: SalesReportsController Endpoints (Upload, OCR, Streaming)

**Files:**
- Modify: `AuditCkDayo/Controllers/SalesReportsController.cs`
- Modify: `AuditCkDayo/ViewModels/SalesReportReviewViewModel.cs`
- Modify: `AuditCkDayo.Tests/SalesReportDepositSlipTests.cs`

- [ ] **Step 1: Write controller endpoint tests**

Add to `AuditCkDayo.Tests/SalesReportDepositSlipTests.cs`:

```csharp
        [Fact]
        public async Task UploadDepositSlip_RequiresVarianceReason_WhenAmountsDoNotMatch()
        {
            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AuditCkDayo.Data.AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditCkDayo.Data.AuditDbContext(options);
            var report = new SalesReport
            {
                Id = 10,
                BusinessDate = DateTime.Today,
                ConfirmedCashToHandover = 50000.00m,
                Status = SalesReportStatus.Confirmed
            };
            context.SalesReports.Add(report);
            context.SaveChanges();

            var controller = new AuditCkDayo.Controllers.SalesReportsController(context, null!, null!);
            // Set user claims for Manager
            var claims = new System.Collections.Generic.List<System.Security.Claims.Claim>
            {
                new(System.Security.Claims.ClaimTypes.NameIdentifier, "1"),
                new(System.Security.Claims.ClaimTypes.Role, "Manager")
            };
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "TestAuth"))
                }
            };

            // Attempt upload with variance but empty reason
            var result = await controller.UploadDepositSlip(new AuditCkDayo.ViewModels.UploadDepositSlipRequest
            {
                SalesReportId = 10,
                DepositedAmount = 48000.00m, // Short by 2000
                DepositBankName = "BDO",
                DepositReferenceNumber = "123",
                DepositVarianceReason = "" // Missing explanation
            }, null);

            var badRequest = Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);
            Assert.Contains("Variance explanation is required", badRequest.Value?.ToString() ?? "");
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter UploadDepositSlip_RequiresVarianceReason_WhenAmountsDoNotMatch
```
Expected: Method `UploadDepositSlip` does not exist.

- [ ] **Step 3: Implement controller endpoints in `SalesReportsController.cs`**

1. Create `UploadDepositSlipRequest` in `SalesReportReviewViewModel.cs`:
```csharp
    public class UploadDepositSlipRequest
    {
        public int SalesReportId { get; set; }
        public decimal DepositedAmount { get; set; }
        public string? DepositBankName { get; set; }
        public string? DepositReferenceNumber { get; set; }
        public DateTime? DepositDate { get; set; }
        public string? DepositVarianceReason { get; set; }
    }
```

2. Add actions to `AuditCkDayo/Controllers/SalesReportsController.cs`:
```csharp
        [HttpPost]
        [Authorize(Roles = "Owner,Manager,Admin")]
        public async Task<IActionResult> ExtractDepositSlipOcr(IFormFile? depositSlipImage, [FromServices] IDepositSlipOcrService ocrService)
        {
            if (depositSlipImage == null || depositSlipImage.Length == 0)
            {
                return BadRequest(new { success = false, message = "Please upload an image file." });
            }

            using var stream = depositSlipImage.OpenReadStream();
            var ocr = await ocrService.ParseDepositSlipAsync(stream);

            return Json(new
            {
                success = ocr.Success,
                detectedBank = ocr.DetectedBank,
                detectedAmount = ocr.DetectedAmount,
                detectedReference = ocr.DetectedReference,
                detectedDate = ocr.DetectedDate?.ToString("yyyy-MM-dd")
            });
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager,Admin")]
        public async Task<IActionResult> UploadDepositSlip([FromForm] UploadDepositSlipRequest request, IFormFile? depositSlipFile)
        {
            var report = await _context.SalesReports.FindAsync(request.SalesReportId);
            if (report == null) return NotFound("Sales report not found.");

            var variance = request.DepositedAmount - report.ConfirmedCashToHandover;
            if (Math.Abs(variance) >= 0.01m && string.IsNullOrWhiteSpace(request.DepositVarianceReason))
            {
                return BadRequest("A Deposit Variance explanation is required when the deposited amount differs from confirmed cash sales.");
            }

            if (depositSlipFile != null && depositSlipFile.Length > 0)
            {
                var uploadsDir = Path.Combine(_env.ContentRootPath, "storage", "deposit_slips");
                if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

                var extension = Path.GetExtension(depositSlipFile.FileName).ToLowerInvariant();
                var uniqueName = $"slip_{report.Id}_{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsDir, uniqueName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await depositSlipFile.CopyToAsync(fileStream);
                }

                report.DepositSlipImageUrl = $"/SalesReports/DepositSlip/{uniqueName}";
            }

            report.DepositedAmount = request.DepositedAmount;
            report.DepositBankName = request.DepositBankName;
            report.DepositReferenceNumber = request.DepositReferenceNumber;
            report.DepositDate = request.DepositDate ?? DateTime.Today;
            report.DepositVarianceReason = request.DepositVarianceReason;
            report.DepositUploadedByUserId = GetCurrentUserId();
            report.DepositUploadedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Deposit slip uploaded and recorded successfully!" });
        }

        [HttpGet("SalesReports/DepositSlip/{fileName}")]
        [Authorize(Roles = "Owner,Manager,Admin,Auditor")]
        public IActionResult GetDepositSlipImage(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains(".."))
            {
                return BadRequest("Invalid file name.");
            }

            var safeName = Path.GetFileName(fileName);
            var filePath = Path.Combine(_env.ContentRootPath, "storage", "deposit_slips", safeName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var contentType = safeName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            return PhysicalFile(filePath, contentType);
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter UploadDepositSlip_RequiresVarianceReason_WhenAmountsDoNotMatch
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add AuditCkDayo/Controllers/SalesReportsController.cs AuditCkDayo/ViewModels/SalesReportReviewViewModel.cs AuditCkDayo.Tests/SalesReportDepositSlipTests.cs
git commit -m "feat(api): add deposit slip upload, OCR extract, and protected image streaming endpoints"
```

---

### Task 5: Treasury Link & Red Alert Indicator for Missing Slips

**Files:**
- Modify: `AuditCkDayo/Controllers/SalesReportsController.cs:815-835` (link `entry.SalesReportId = report.Id`)
- Modify: `AuditCkDayo/Controllers/TreasuryController.cs` (eager load `SalesReport`)
- Modify: `AuditCkDayo/Views/Treasury/Index.cshtml` (red highlight row, `NO DEPOSIT SLIP` badge, warning banner)
- Modify: `AuditCkDayo.Tests/SalesReportDepositSlipTests.cs`

- [ ] **Step 1: Write unit test for Treasury missing deposit slip detection**

Add to `AuditCkDayo.Tests/SalesReportDepositSlipTests.cs`:

```csharp
        [Fact]
        public void TreasuryCashFlowEntry_IdentifiesMissingDepositSlipState()
        {
            var reportWithoutSlip = new SalesReport
            {
                Id = 1,
                ConfirmedCashToHandover = 30000m,
                DepositSlipImageUrl = null
            };

            var entryMissing = new CashFlowEntry
            {
                Category = CashFlowCategory.Sales,
                Direction = CashFlowDirection.In,
                Amount = 30000m,
                SalesReport = reportWithoutSlip
            };

            Assert.NotNull(entryMissing.SalesReport);
            Assert.False(entryMissing.SalesReport.HasDepositSlip);

            // Once slip is attached
            reportWithoutSlip.DepositSlipImageUrl = "/SalesReports/DepositSlip/slip1.jpg";
            Assert.True(entryMissing.SalesReport.HasDepositSlip);
        }
```

- [ ] **Step 2: Run test to verify it passes**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter TreasuryCashFlowEntry_IdentifiesMissingDepositSlipState
```
Expected: PASS.

- [ ] **Step 3: Update `PostConfirmedSalesReportToTreasuryAsync` in `SalesReportsController.cs`**

In `AuditCkDayo/Controllers/SalesReportsController.cs:820-830`:
```csharp
            entry.TreasuryCashFlow = flow;
            entry.TreasuryCashFlowId = flow.Id;
            entry.Direction = CashFlowDirection.In;
            entry.Category = CashFlowCategory.Sales;
            entry.EstablishmentId = report.EstablishmentId;
            entry.SalesReportId = report.Id; // <-- DIRECT LINK ADDED
            entry.SourceDocumentId = report.DocumentRecordId;
            entry.Amount = report.ConfirmedCashToHandover;
```

- [ ] **Step 4: Update `TreasuryController.cs` to include `SalesReport`**

In `AuditCkDayo/Controllers/TreasuryController.cs`:
Ensure EF query includes `.Include(f => f.Entries).ThenInclude(e => e.SalesReport)` so `SalesReport.HasDepositSlip` is accessible in the view.

- [ ] **Step 5: Update `AuditCkDayo/Views/Treasury/Index.cshtml` with Red Warning & Banner**

In `AuditCkDayo/Views/Treasury/Index.cshtml`:
1. Top Banner:
```razor
@{
    var unDepositedSales = Model.Entries.Where(e => e.Direction == CashFlowDirection.In 
        && e.Category == CashFlowCategory.Sales 
        && (e.SalesReport == null || !e.SalesReport.HasDepositSlip)).ToList();
}
@if (unDepositedSales.Any())
{
    <div class="p-4 bg-red-500/10 border border-red-500/30 rounded-xl flex items-center justify-between text-xs text-red-400 font-bold mb-4 shadow-sm">
        <div class="flex items-center gap-2">
            <span class="material-symbols-outlined text-[18px]">warning</span>
            <span>ATTENTION: @unDepositedSales.Count Sales Handover(s) totaling ₱@unDepositedSales.Sum(s => s.Amount).ToString("N2") are awaiting bank deposit slips.</span>
        </div>
        <span class="px-2 py-0.5 rounded bg-red-500/20 text-red-300 font-mono">PROOF REQUIRED</span>
    </div>
}
```

2. Inside the Sales Rows loop:
```razor
@{
    var hasSlip = entry.SalesReport != null && entry.SalesReport.HasDepositSlip;
    var rowClass = !hasSlip ? "bg-red-500/10 border-l-4 border-red-500" : "";
}
<div class="grid grid-cols-12 px-3 py-2 items-center text-xs @rowClass">
    <!-- Existing columns -->
    <div class="col-span-8 flex items-center gap-2">
        <span>@entry.Notes</span>
        @if (!hasSlip)
        {
            <span class="px-2 py-0.5 text-[9px] font-bold rounded-full bg-red-600 text-white animate-pulse">● NO DEPOSIT SLIP</span>
            <button type="button" onclick="openDepositModal(@entry.SalesReportId)" class="text-primary hover:underline text-[10px] font-bold ml-1">Upload Slip</button>
        }
        else
        {
            <a href="@entry.SalesReport.DepositSlipImageUrl" target="_blank" class="px-2 py-0.5 text-[9px] font-bold rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 inline-flex items-center gap-1">
                <span class="material-symbols-outlined text-[12px]">receipt</span> Deposited (@entry.SalesReport.DepositBankName)
            </a>
        }
    </div>
    <!-- Amount column -->
    <div class="col-span-4 text-right font-mono font-bold @(!hasSlip ? "text-red-400" : "text-on-surface")">
        ₱@entry.Amount.ToString("N2")
    </div>
</div>
```

- [ ] **Step 6: Run full test suite to verify no regressions**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj
```
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add AuditCkDayo/Controllers/SalesReportsController.cs AuditCkDayo/Controllers/TreasuryController.cs AuditCkDayo/Views/Treasury/Index.cshtml AuditCkDayo.Tests/SalesReportDepositSlipTests.cs
git commit -m "feat(treasury): highlight un-deposited sales inflows in red with warning alert badge"
```

---

### Task 6: UI Additions in Sales Reports List & Review Page

**Files:**
- Modify: `AuditCkDayo/Views/SalesReports/Index.cshtml`
- Modify: `AuditCkDayo/Views/SalesReports/ReviewManager.cshtml`

- [ ] **Step 1: Add "Deposit Slip" column and Quick-Upload Modal to `Index.cshtml`**

In `AuditCkDayo/Views/SalesReports/Index.cshtml`:
1. Add `<th>DEPOSIT SLIP</th>` to the table header.
2. In the table body loop:
```razor
<td class="px-4 py-3 text-xs">
    @if (report.Status == SalesReportStatus.Confirmed)
    {
        @if (!report.HasDepositSlip)
        {
            <div class="flex items-center gap-2">
                <span class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-amber-500/20 text-amber-300 border border-amber-500/30">
                    Awaiting Slip
                </span>
                <button type="button" onclick="openDepositModal(@report.Id, @report.ConfirmedCashToHandover)" class="px-2 py-1 rounded bg-primary text-white text-[10px] font-bold hover:brightness-110">
                    Upload
                </button>
            </div>
        }
        else if (report.IsDepositMatched)
        {
            <a href="@report.DepositSlipImageUrl" target="_blank" class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 inline-flex items-center gap-1 hover:underline">
                <span class="material-symbols-outlined text-[13px]">check_circle</span> Matched (₱@report.DepositedAmount?.ToString("N2"))
            </a>
        }
        else
        {
            <a href="@report.DepositSlipImageUrl" target="_blank" class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-red-500/20 text-red-400 border border-red-500/30 inline-flex items-center gap-1 hover:underline" title="@report.DepositVarianceReason">
                <span class="material-symbols-outlined text-[13px]">warning</span> Diff (@(report.DepositVariance > 0 ? "+" : "")₱@report.DepositVariance.ToString("N2"))
            </a>
        }
    }
    else
    {
        <span class="text-slate-500 text-[11px]">—</span>
    }
</td>
```
3. Add the compact Quick-Upload Modal dialog at the bottom of the page with file upload, OCR auto-fill, and live variance calculation.

- [ ] **Step 2: Add "Bank Deposit Verification" card to `ReviewManager.cshtml`**

In `AuditCkDayo/Views/SalesReports/ReviewManager.cshtml`:
Add a card with:
- Image selector / dropzone for deposit slip.
- Auto-fill OCR button calling `/SalesReports/ExtractDepositSlipOcr`.
- Form inputs: Bank Name, Amount, Reference #, Date, Variance Reason.
- Dynamic comparison banner: Handover Cash vs Deposited Amount $\rightarrow$ Balanced or Discrepancy warning.

- [ ] **Step 3: Test compilation and view rendering**

Run:
```bash
dotnet build AuditCkDayo/AuditCkDayo.csproj
```
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add AuditCkDayo/Views/SalesReports/Index.cshtml AuditCkDayo/Views/SalesReports/ReviewManager.cshtml
git commit -m "feat(ui): add deposit slip column, quick-upload modal, and review card"
```

---

### Task 7: Full Test Suite Verification & Sanity Check

**Files:**
- Test: `AuditCkDayo.Tests/AuditCkDayo.Tests.csproj`

- [ ] **Step 1: Run complete test suite**

Run:
```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release
```
Expected: All tests pass with 0 failures.

- [ ] **Step 2: Verify git status is clean**

Run:
```bash
git status
```
Expected: Clean working tree on branch `main`.

- [ ] **Step 3: Final Commit & Tag**

```bash
git commit --allow-empty -m "chore(release): complete sales report deposit slip verification and treasury alert"
```
