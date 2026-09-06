# Treasury Sales Unconfirm Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a manager-accessible Unconfirm Sales Report action inside the existing Treasury Edit Entry page so confirmed sales cash-ins can be removed from Treasury and their source sales report can return to draft for branch edits.

**Architecture:** Keep the feature inside `TreasuryController` because the user starts from the Treasury cash-flow entry. Add small view-model state through `ViewBag` for the existing `CashFlowEntry` edit view, and add one POST action that performs the unlock in a transaction. Reuse existing domain states: `SalesReportStatus.Draft`, `DocumentReviewStatus.Draft`, and `TreasuryCashFlow.RecomputeTotals()`.

**Tech Stack:** ASP.NET Core MVC on .NET 9, Entity Framework Core 9, Razor views, xUnit with SQLite in-memory EF Core contexts.

---

## File Structure

- Modify `AuditCkDayo/Controllers/TreasuryController.cs`
  - Add helper logic to detect whether a `CashFlowEntry` is an unconfirmable sales cash-in.
  - Extend `EditEntry(int id)` to populate view state for the button.
  - Add `UnconfirmSalesReport(int id)` POST action.
  - Keep all data changes in one EF transaction.

- Modify `AuditCkDayo/Views/Treasury/EditEntry.cshtml`
  - Add a danger section below the existing edit form.
  - Render the section only when the controller sets `ViewBag.CanUnconfirmSalesReport = true`.
  - Post to `Treasury/UnconfirmSalesReport` with anti-forgery token and entry id.

- Modify `AuditCkDayo.Tests/UnitTest1.cs`
  - Add focused tests near the existing `TreasuryControllerTests` region.
  - Cover the happy path, closed-flow block, non-sales-entry block, and view flag population.

---

### Task 1: Add Failing Treasury Unconfirm Happy-Path Test

**Files:**
- Modify: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Locate the existing treasury test class**

Find `TreasuryControllerTests` in `AuditCkDayo.Tests/UnitTest1.cs`. It already contains helper methods similar to:

```csharp
private static TreasuryController CreateController(AuditDbContext context)
{
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, "1"),
        new Claim(ClaimTypes.Role, "Manager")
    };
    var identity = new ClaimsIdentity(claims, "TestAuth");
    var principal = new ClaimsPrincipal(identity);
    var httpContext = new DefaultHttpContext { User = principal };
    var tempDataProvider = new FakeTempDataProvider();
    var tempData = new TempDataDictionary(httpContext, tempDataProvider);

    return new TreasuryController(context)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        },
        TempData = tempData
    };
}
```

Use the existing helper if present. Do not duplicate it unless the signatures differ.

- [ ] **Step 2: Add the failing test**

Add this test inside `TreasuryControllerTests`:

```csharp
[Fact]
public async Task UnconfirmSalesReport_RemovesSalesCashInAndReturnsReportToDraft()
{
    using var context = new AuditDbContext(_options);

    var manager = new User
    {
        Id = 1,
        Name = "Manager Mina",
        Email = "manager@test.com",
        PasswordHash = "hash",
        Role = UserRole.Manager
    };
    var branch = new Establishment
    {
        Id = 4,
        Name = "CKR Branch 4",
        IsOperatingBranch = true,
        IsActive = true
    };
    var document = new DocumentRecord
    {
        Id = 292,
        DocumentType = DocumentType.DailySalesReport,
        UploadedByUserId = manager.Id,
        UploadedByUser = manager,
        UploadedAt = new DateTime(2026, 9, 6, 4, 0, 0, DateTimeKind.Utc),
        ImageUrl = "/SalesReports/Image/sample.jpg",
        OcrStatus = OcrStatus.Parsed,
        ReviewStatus = DocumentReviewStatus.Confirmed,
        ConfirmedByUserId = manager.Id,
        ConfirmedAt = new DateTime(2026, 9, 6, 4, 49, 55, DateTimeKind.Utc)
    };
    var report = new SalesReport
    {
        Id = 279,
        DocumentRecordId = document.Id,
        DocumentRecord = document,
        EstablishmentId = branch.Id,
        Establishment = branch,
        BusinessDate = new DateTime(2026, 9, 5),
        HandoverDate = new DateTime(2026, 9, 6),
        GrossSales = 23002m,
        ConfirmedCashToHandover = 23002m,
        Status = SalesReportStatus.Confirmed,
        ConfirmedByUserId = manager.Id,
        ConfirmedAt = new DateTime(2026, 9, 6, 4, 49, 55, DateTimeKind.Utc)
    };
    var flow = new TreasuryCashFlow
    {
        Id = 63,
        TreasuryUserId = manager.Id,
        TreasuryUser = manager,
        CashFlowDate = new DateTime(2026, 9, 6),
        StartingBalance = 79811.25m,
        Status = TreasuryCashFlowStatus.Open
    };
    var salesEntry = new CashFlowEntry
    {
        Id = 1082,
        TreasuryCashFlowId = flow.Id,
        TreasuryCashFlow = flow,
        Direction = CashFlowDirection.In,
        Category = CashFlowCategory.Sales,
        EstablishmentId = branch.Id,
        Establishment = branch,
        SourceDocumentId = document.Id,
        SourceDocument = document,
        Amount = 23002m,
        Notes = "Sales handover for 2026-09-05",
        CreatedByUserId = manager.Id,
        CreatedByUser = manager,
        ConfirmedByUserId = manager.Id
    };
    var pcfEntry = new CashFlowEntry
    {
        Id = 1083,
        TreasuryCashFlowId = flow.Id,
        TreasuryCashFlow = flow,
        Direction = CashFlowDirection.Out,
        Category = CashFlowCategory.PcfRelease,
        EstablishmentId = branch.Id,
        Establishment = branch,
        Amount = 5000m,
        Notes = "PCF",
        CreatedByUserId = manager.Id,
        CreatedByUser = manager,
        ConfirmedByUserId = manager.Id
    };

    flow.Entries.Add(salesEntry);
    flow.Entries.Add(pcfEntry);
    flow.RecomputeTotals();

    context.Users.Add(manager);
    context.Establishments.Add(branch);
    context.DocumentRecords.Add(document);
    context.SalesReports.Add(report);
    context.TreasuryCashFlows.Add(flow);
    context.CashFlowEntries.AddRange(salesEntry, pcfEntry);
    await context.SaveChangesAsync();

    var controller = CreateController(context);

    var result = await controller.UnconfirmSalesReport(salesEntry.Id);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal("Index", redirect.ActionName);
    Assert.Equal(new DateTime(2026, 9, 6), redirect.RouteValues!["date"]);

    Assert.Null(await context.CashFlowEntries.FindAsync(salesEntry.Id));
    Assert.NotNull(await context.CashFlowEntries.FindAsync(pcfEntry.Id));

    var savedReport = await context.SalesReports.SingleAsync(r => r.Id == report.Id);
    Assert.Equal(SalesReportStatus.Draft, savedReport.Status);
    Assert.Null(savedReport.ConfirmedByUserId);
    Assert.Null(savedReport.ConfirmedAt);

    var savedDocument = await context.DocumentRecords.SingleAsync(d => d.Id == document.Id);
    Assert.Equal(DocumentReviewStatus.Draft, savedDocument.ReviewStatus);
    Assert.Null(savedDocument.ConfirmedByUserId);
    Assert.Null(savedDocument.ConfirmedAt);

    var savedFlow = await context.TreasuryCashFlows
        .Include(f => f.Entries)
        .SingleAsync(f => f.Id == flow.Id);
    Assert.Equal(0m, savedFlow.TotalCashIn);
    Assert.Equal(5000m, savedFlow.TotalCashOut);
    Assert.Equal(79811.25m, savedFlow.NetCashFlow);
    Assert.Equal(74811.25m, savedFlow.ClosingBalance);
    Assert.Equal("Sales report unconfirmed. Treasury cash-in was removed and the branch can edit the report again.", controller.TempData["Message"]);
}
```

- [ ] **Step 3: Run the failing test**

Run:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~UnconfirmSalesReport_RemovesSalesCashInAndReturnsReportToDraft"
```

Expected result before implementation:

```text
error CS1061: 'TreasuryController' does not contain a definition for 'UnconfirmSalesReport'
```

---

### Task 2: Implement Controller Unconfirm Action

**Files:**
- Modify: `AuditCkDayo/Controllers/TreasuryController.cs`

- [ ] **Step 1: Add the POST action after `DeleteEntry`**

Insert this method after `DeleteEntry` and before `PopulateReleasePcfLookupsAsync`:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UnconfirmSalesReport(int id)
{
    if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
    {
        return Unauthorized();
    }

    await using var transaction = await _context.Database.BeginTransactionAsync();

    var entry = await _context.CashFlowEntries
        .Include(e => e.TreasuryCashFlow)
            .ThenInclude(f => f.Entries)
        .Include(e => e.SourceDocument)
        .FirstOrDefaultAsync(e => e.Id == id);

    if (entry == null)
    {
        TempData["Error"] = "Treasury entry not found.";
        return RedirectToAction(nameof(Index));
    }

    var flowDate = entry.TreasuryCashFlow.CashFlowDate;

    if (entry.TreasuryCashFlow.Status == TreasuryCashFlowStatus.Closed)
    {
        TempData["Error"] = "This entry belongs to a closed/locked treasury day and cannot be unconfirmed.";
        return RedirectToAction(nameof(Index), new { date = flowDate });
    }

    if (!IsSalesReportCashIn(entry))
    {
        TempData["Error"] = "Only linked sales cash-in entries can be unconfirmed from Treasury.";
        return RedirectToAction(nameof(EditEntry), new { id = entry.Id });
    }

    var report = await _context.SalesReports
        .Include(r => r.DocumentRecord)
        .FirstOrDefaultAsync(r => r.DocumentRecordId == entry.SourceDocumentId.Value);

    if (report == null)
    {
        TempData["Error"] = "Linked sales report not found.";
        return RedirectToAction(nameof(EditEntry), new { id = entry.Id });
    }

    if (report.Status != SalesReportStatus.Confirmed && report.DocumentRecord.ReviewStatus != DocumentReviewStatus.Confirmed)
    {
        TempData["Error"] = "Only confirmed sales reports can be unconfirmed.";
        return RedirectToAction(nameof(EditEntry), new { id = entry.Id });
    }

    _context.CashFlowEntries.Remove(entry);

    report.Status = SalesReportStatus.Draft;
    report.ConfirmedByUserId = null;
    report.ConfirmedAt = null;
    report.DocumentRecord.ReviewStatus = DocumentReviewStatus.Draft;
    report.DocumentRecord.ConfirmedByUserId = null;
    report.DocumentRecord.ConfirmedAt = null;

    entry.TreasuryCashFlow.Entries.Remove(entry);
    entry.TreasuryCashFlow.RecomputeTotals();

    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    TempData["Message"] = "Sales report unconfirmed. Treasury cash-in was removed and the branch can edit the report again.";
    return RedirectToAction(nameof(Index), new { date = flowDate });
}
```

- [ ] **Step 2: Add the sales-cash-in predicate helper**

Add this private helper near the other private helpers in `TreasuryController`:

```csharp
private static bool IsSalesReportCashIn(CashFlowEntry entry)
{
    return entry.Direction == CashFlowDirection.In
        && entry.Category == CashFlowCategory.Sales
        && entry.SourceDocumentId.HasValue
        && entry.SourceDocument?.DocumentType == DocumentType.DailySalesReport;
}
```

- [ ] **Step 3: Run the happy-path test**

Run:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~UnconfirmSalesReport_RemovesSalesCashInAndReturnsReportToDraft"
```

Expected result:

```text
Passed!  - Failed: 0, Passed: 1
```

---

### Task 3: Add Tests for Blocked Cases

**Files:**
- Modify: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Add closed-flow test**

Add this test inside `TreasuryControllerTests`:

```csharp
[Fact]
public async Task UnconfirmSalesReport_DoesNotChangeClosedTreasuryFlow()
{
    using var context = new AuditDbContext(_options);

    var manager = new User { Id = 1, Name = "Manager", Email = "manager@test.com", PasswordHash = "hash", Role = UserRole.Manager };
    var document = new DocumentRecord
    {
        Id = 501,
        DocumentType = DocumentType.DailySalesReport,
        UploadedByUserId = manager.Id,
        ImageUrl = "/sales.jpg",
        OcrStatus = OcrStatus.Parsed,
        ReviewStatus = DocumentReviewStatus.Confirmed,
        ConfirmedByUserId = manager.Id,
        ConfirmedAt = DateTime.UtcNow
    };
    var report = new SalesReport
    {
        Id = 502,
        DocumentRecordId = document.Id,
        EstablishmentId = 1,
        BusinessDate = new DateTime(2026, 9, 5),
        HandoverDate = new DateTime(2026, 9, 6),
        Status = SalesReportStatus.Confirmed,
        ConfirmedByUserId = manager.Id,
        ConfirmedAt = DateTime.UtcNow
    };
    var flow = new TreasuryCashFlow
    {
        Id = 503,
        TreasuryUserId = manager.Id,
        CashFlowDate = new DateTime(2026, 9, 6),
        StartingBalance = 1000m,
        Status = TreasuryCashFlowStatus.Closed
    };
    var entry = new CashFlowEntry
    {
        Id = 504,
        TreasuryCashFlowId = flow.Id,
        TreasuryCashFlow = flow,
        Direction = CashFlowDirection.In,
        Category = CashFlowCategory.Sales,
        SourceDocumentId = document.Id,
        SourceDocument = document,
        Amount = 500m,
        CreatedByUserId = manager.Id,
        CreatedByUser = manager
    };

    flow.Entries.Add(entry);
    flow.RecomputeTotals();
    context.Users.Add(manager);
    context.DocumentRecords.Add(document);
    context.SalesReports.Add(report);
    context.TreasuryCashFlows.Add(flow);
    context.CashFlowEntries.Add(entry);
    await context.SaveChangesAsync();

    var controller = CreateController(context);

    var result = await controller.UnconfirmSalesReport(entry.Id);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal("Index", redirect.ActionName);
    Assert.Equal(new DateTime(2026, 9, 6), redirect.RouteValues!["date"]);
    Assert.NotNull(await context.CashFlowEntries.FindAsync(entry.Id));
    Assert.Equal(SalesReportStatus.Confirmed, (await context.SalesReports.FindAsync(report.Id))!.Status);
    Assert.Equal("This entry belongs to a closed/locked treasury day and cannot be unconfirmed.", controller.TempData["Error"]);
}
```

- [ ] **Step 2: Add non-sales-entry test**

Add this test inside `TreasuryControllerTests`:

```csharp
[Fact]
public async Task UnconfirmSalesReport_DoesNotChangeNonSalesEntry()
{
    using var context = new AuditDbContext(_options);

    var manager = new User { Id = 1, Name = "Manager", Email = "manager@test.com", PasswordHash = "hash", Role = UserRole.Manager };
    var flow = new TreasuryCashFlow
    {
        Id = 601,
        TreasuryUserId = manager.Id,
        CashFlowDate = new DateTime(2026, 9, 6),
        StartingBalance = 1000m,
        Status = TreasuryCashFlowStatus.Open
    };
    var entry = new CashFlowEntry
    {
        Id = 602,
        TreasuryCashFlowId = flow.Id,
        TreasuryCashFlow = flow,
        Direction = CashFlowDirection.In,
        Category = CashFlowCategory.ChangePcf,
        Amount = 300m,
        Notes = "Change PCF surrendered",
        CreatedByUserId = manager.Id,
        CreatedByUser = manager
    };

    flow.Entries.Add(entry);
    flow.RecomputeTotals();
    context.Users.Add(manager);
    context.TreasuryCashFlows.Add(flow);
    context.CashFlowEntries.Add(entry);
    await context.SaveChangesAsync();

    var controller = CreateController(context);

    var result = await controller.UnconfirmSalesReport(entry.Id);

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal("EditEntry", redirect.ActionName);
    Assert.Equal(entry.Id, redirect.RouteValues!["id"]);
    Assert.NotNull(await context.CashFlowEntries.FindAsync(entry.Id));
    Assert.Equal("Only linked sales cash-in entries can be unconfirmed from Treasury.", controller.TempData["Error"]);
}
```

- [ ] **Step 3: Run blocked-case tests**

Run:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~UnconfirmSalesReport_DoesNotChange"
```

Expected result:

```text
Passed!  - Failed: 0, Passed: 2
```

---

### Task 4: Populate Edit Page Visibility State

**Files:**
- Modify: `AuditCkDayo/Controllers/TreasuryController.cs`
- Modify: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Add failing GET edit visibility test**

Add this test inside `TreasuryControllerTests`:

```csharp
[Fact]
public async Task EditEntry_SetsCanUnconfirmSalesReportForConfirmedSalesCashIn()
{
    using var context = new AuditDbContext(_options);

    var manager = new User { Id = 1, Name = "Manager", Email = "manager@test.com", PasswordHash = "hash", Role = UserRole.Manager };
    var document = new DocumentRecord
    {
        Id = 701,
        DocumentType = DocumentType.DailySalesReport,
        UploadedByUserId = manager.Id,
        ImageUrl = "/sales.jpg",
        OcrStatus = OcrStatus.Parsed,
        ReviewStatus = DocumentReviewStatus.Confirmed
    };
    var report = new SalesReport
    {
        Id = 702,
        DocumentRecordId = document.Id,
        EstablishmentId = 1,
        BusinessDate = new DateTime(2026, 9, 5),
        HandoverDate = new DateTime(2026, 9, 6),
        Status = SalesReportStatus.Confirmed
    };
    var flow = new TreasuryCashFlow
    {
        Id = 703,
        TreasuryUserId = manager.Id,
        CashFlowDate = new DateTime(2026, 9, 6),
        StartingBalance = 1000m,
        Status = TreasuryCashFlowStatus.Open
    };
    var entry = new CashFlowEntry
    {
        Id = 704,
        TreasuryCashFlowId = flow.Id,
        TreasuryCashFlow = flow,
        Direction = CashFlowDirection.In,
        Category = CashFlowCategory.Sales,
        SourceDocumentId = document.Id,
        SourceDocument = document,
        Amount = 500m,
        CreatedByUserId = manager.Id,
        CreatedByUser = manager
    };

    context.Users.Add(manager);
    context.DocumentRecords.Add(document);
    context.SalesReports.Add(report);
    context.TreasuryCashFlows.Add(flow);
    context.CashFlowEntries.Add(entry);
    await context.SaveChangesAsync();

    var controller = CreateController(context);

    var result = await controller.EditEntry(entry.Id);

    Assert.IsType<ViewResult>(result);
    Assert.True((bool)controller.ViewBag.CanUnconfirmSalesReport);
    Assert.Equal(report.Id, controller.ViewBag.LinkedSalesReportId);
}
```

- [ ] **Step 2: Update GET `EditEntry` query**

Change the query inside `EditEntry(int id)` from:

```csharp
var entry = await _context.CashFlowEntries
    .Include(e => e.TreasuryCashFlow)
    .FirstOrDefaultAsync(e => e.Id == id);
```

to:

```csharp
var entry = await _context.CashFlowEntries
    .Include(e => e.TreasuryCashFlow)
    .Include(e => e.SourceDocument)
    .FirstOrDefaultAsync(e => e.Id == id);
```

- [ ] **Step 3: Add view-state population before returning the view**

In `EditEntry(int id)`, after `ViewBag.FlowDate = entry.TreasuryCashFlow.CashFlowDate;`, add:

```csharp
ViewBag.CanUnconfirmSalesReport = false;
ViewBag.LinkedSalesReportId = null;

if (IsSalesReportCashIn(entry))
{
    var linkedReport = await _context.SalesReports
        .AsNoTracking()
        .Where(r => r.DocumentRecordId == entry.SourceDocumentId!.Value)
        .Select(r => new
        {
            r.Id,
            r.Status,
            ReviewStatus = r.DocumentRecord.ReviewStatus
        })
        .FirstOrDefaultAsync();

    ViewBag.CanUnconfirmSalesReport = linkedReport != null
        && entry.TreasuryCashFlow.Status != TreasuryCashFlowStatus.Closed
        && (linkedReport.Status == SalesReportStatus.Confirmed
            || linkedReport.ReviewStatus == DocumentReviewStatus.Confirmed);
    ViewBag.LinkedSalesReportId = linkedReport?.Id;
}
```

- [ ] **Step 4: Run the visibility test**

Run:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~EditEntry_SetsCanUnconfirmSalesReportForConfirmedSalesCashIn"
```

Expected result:

```text
Passed!  - Failed: 0, Passed: 1
```

---

### Task 5: Add Unconfirm Button to Treasury Edit View

**Files:**
- Modify: `AuditCkDayo/Views/Treasury/EditEntry.cshtml`
- Modify: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Add view-content test**

Add this test near existing Razor view text tests:

```csharp
[Fact]
public void TreasuryEditEntry_ViewContainsUnconfirmSalesReportForm()
{
    var viewPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "AuditCkDayo",
        "Views",
        "Treasury",
        "EditEntry.cshtml"));

    var view = File.ReadAllText(viewPath);

    Assert.Contains("CanUnconfirmSalesReport", view);
    Assert.Contains("UnconfirmSalesReport", view);
    Assert.Contains("Unconfirm Sales Report", view);
    Assert.Contains("This will remove the sales cash-in from Treasury", view);
}
```

- [ ] **Step 2: Add local Razor variables**

At the top of `AuditCkDayo/Views/Treasury/EditEntry.cshtml`, inside the existing Razor block, change:

```csharp
var flowDate = ViewBag.FlowDate as DateTime? ?? DateTime.Today;
```

to:

```csharp
var flowDate = ViewBag.FlowDate as DateTime? ?? DateTime.Today;
var canUnconfirmSalesReport = ViewBag.CanUnconfirmSalesReport as bool? ?? false;
var linkedSalesReportId = ViewBag.LinkedSalesReportId as int?;
```

- [ ] **Step 3: Add the danger section after the existing edit card**

Insert this block after the existing `</div>` that closes the edit-form card and before the outer wrapper closes:

```cshtml
@if (canUnconfirmSalesReport)
{
    <section class="rounded-xl border border-red-300 bg-red-50 p-6 shadow-sm">
        <div class="space-y-3">
            <div>
                <h2 class="text-lg font-semibold text-red-900">Sales report confirmation</h2>
                <p class="mt-1 text-sm text-red-800">
                    This cash-in came from a confirmed sales report. Unconfirming it removes this cash-in from Treasury and lets the branch edit the sales report again.
                </p>
            </div>

            @if (linkedSalesReportId.HasValue)
            {
                <p class="text-xs text-red-700">
                    Linked sales report: #@linkedSalesReportId.Value
                </p>
            }

            <form asp-controller="Treasury" asp-action="UnconfirmSalesReport" method="post">
                @Html.AntiForgeryToken()
                <input type="hidden" name="id" value="@Model.Id" />
                <button type="submit"
                        class="rounded-lg bg-red-700 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-red-800"
                        onclick="return confirm('This will remove the sales cash-in from Treasury and send the sales report back to draft so the branch can edit it. The manager must confirm it again after editing. Continue?')">
                    Unconfirm Sales Report
                </button>
            </form>
        </div>
    </section>
}
```

- [ ] **Step 4: Run the view-content test**

Run:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~TreasuryEditEntry_ViewContainsUnconfirmSalesReportForm"
```

Expected result:

```text
Passed!  - Failed: 0, Passed: 1
```

---

### Task 6: Run Focused Treasury Regression Tests

**Files:**
- No source edits.

- [ ] **Step 1: Run all unconfirm-focused tests**

Run:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~UnconfirmSalesReport|FullyQualifiedName~EditEntry_SetsCanUnconfirmSalesReport|FullyQualifiedName~TreasuryEditEntry_ViewContainsUnconfirmSalesReportForm"
```

Expected result:

```text
Passed!  - Failed: 0, Passed: 4
```

- [ ] **Step 2: Run existing treasury controller tests**

Run:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~TreasuryControllerTests"
```

Expected result:

```text
Passed!  - Failed: 0
```

- [ ] **Step 3: Run full test suite**

Run:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --no-restore
```

Expected result:

```text
Passed!  - Failed: 0
```

Known existing warnings may still appear for `SixLabors.ImageSharp 3.1.5` vulnerabilities. Do not treat those warnings as caused by this feature.

---

### Task 7: Manual Smoke Test in Browser

**Files:**
- No source edits.

- [ ] **Step 1: Start the app**

Run:

```bash
dotnet run --project AuditCkDayo/AuditCkDayo.csproj
```

Expected:

```text
Now listening on: http://localhost:<port>
```

- [ ] **Step 2: Log in as a manager/admin/owner**

Use an existing seeded account from `README.md`, for example:

```text
alice@test.com / Password123!
```

- [ ] **Step 3: Create or find a confirmed sales cash-in**

Use an existing development database record or create a sales report and confirm it through the existing sales report flow. Confirming must create a `CashFlowEntry` with:

```text
Direction = In
Category = Sales
SourceDocumentId = linked sales document id
```

- [ ] **Step 4: Open Treasury for the handover date**

Navigate to:

```text
/Treasury?date=<handover-date>
```

Expected:

- The confirmed sales amount appears under cash-in.
- The row has the existing Edit action.

- [ ] **Step 5: Open Edit Entry**

Click Edit for the sales cash-in entry.

Expected:

- Existing edit fields remain visible.
- New `Sales report confirmation` danger section is visible.
- `Unconfirm Sales Report` button is visible.

- [ ] **Step 6: Click Unconfirm Sales Report**

Accept the confirmation dialog.

Expected:

- Redirects back to Treasury page for the same date.
- Success message appears.
- Sales cash-in row is gone.
- Treasury totals are lower by the removed sales amount.

- [ ] **Step 7: Verify branch edit path**

Open the linked sales report through Sales Reports.

Expected:

- Report is no longer confirmed.
- Branch/user with edit permission can edit the report through the existing flow.
- Manager can confirm again after edits.

---

## Plan Self-Review

- Spec coverage: covers Treasury Edit Entry placement, manager-only unlock, linked sales cash-in deletion, sales/document draft reset, treasury total recompute, redirect back to Treasury, and reconfirmation through existing flow.
- Placeholder scan: no TBD/TODO placeholders remain.
- Type consistency: uses existing `CashFlowEntry`, `TreasuryCashFlow`, `SalesReport`, `DocumentRecord`, `SalesReportStatus`, `DocumentReviewStatus`, `CashFlowCategory`, and `CashFlowDirection` names verified in code.
- Scope: focused feature; no new tables, no branch request workflow, no closed-day override.
