# Audit Viewer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a full-screen audit viewer modal to branch delivery verification and manager approval, and replace the harsh pending color with a theme-aligned muted blue.

**Architecture:** Keep the current controller and queue-page workflow. Add the modal directly to both Razor views using the existing row `data-*` payload and receipt route. Update the global Tailwind color token so all pending indicators inherit the calmer theme color.

**Tech Stack:** ASP.NET Core MVC, Razor views, Tailwind CDN config, xUnit controller tests, browser smoke verification.

---

## File Structure

- Modify `AuditCkDayo/Views/Shared/_Layout.cshtml`: change `audit-pending` color token from harsh amber to muted blue.
- Modify `AuditCkDayo/Views/Audits/BranchVerifyList.cshtml`: add `View Full Audit` button, modal markup, modal update/open/close JavaScript.
- Modify `AuditCkDayo/Views/Audits/VerifyList.cshtml`: add the same viewer for manager approval.
- Modify `AuditCkDayo.Tests/UnitTest1.cs`: add regression tests that controller queue models still include receipt details needed by the viewer.

## Task 1: Regression tests for verification queues

**Files:**
- Modify: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Write failing tests**

Add tests in `AuditsControllerTests`:

```csharp
[Fact]
public async Task BranchVerifyList_LoadsDetailsForFullAuditViewer()
{
    using (var context = new AuditDbContext(_options))
    {
        await SeedDataAsync(context);
        context.AuditItems.Add(new AuditItem
        {
            Id = 90,
            BuyerId = 3,
            EstablishmentId = 1,
            Amount = 31m,
            Description = "Viewer branch receipt",
            EntryDate = DateTime.Today,
            ReceiptImageUrl = "/Audits/Receipt/viewer-branch.png",
            Status = AuditStatus.AwaitingBranchVerification,
            Details = new List<AuditItemDetail>
            {
                new AuditItemDetail { ItemName = "Paper", Quantity = 2, Price = 10m, Total = 20m }
            }
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 5, "BranchStaff");
        var result = await controller.BranchVerifyList();

        var view = Assert.IsType<ViewResult>(result);
        var audits = Assert.IsAssignableFrom<IEnumerable<AuditItem>>(view.Model);
        var audit = Assert.Single(audits, a => a.Id == 90);
        Assert.Equal("/Audits/Receipt/viewer-branch.png", audit.ReceiptImageUrl);
        Assert.Contains(audit.Details, d => d.ItemName == "Paper");
    }
}

[Fact]
public async Task VerifyList_LoadsDetailsForFullAuditViewer()
{
    using (var context = new AuditDbContext(_options))
    {
        await SeedDataAsync(context);
        context.AuditItems.Add(new AuditItem
        {
            Id = 91,
            BuyerId = 3,
            EstablishmentId = 1,
            Amount = 42m,
            Description = "Viewer manager receipt",
            EntryDate = DateTime.Today,
            ReceiptImageUrl = "/Audits/Receipt/viewer-manager.png",
            Status = AuditStatus.AwaitingManagerApproval,
            Details = new List<AuditItemDetail>
            {
                new AuditItemDetail { ItemName = "Ink", Quantity = 1, Price = 42m, Total = 42m }
            }
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, 2, "Manager");
        var result = await controller.VerifyList();

        var view = Assert.IsType<ViewResult>(result);
        var audits = Assert.IsAssignableFrom<IEnumerable<AuditItem>>(view.Model);
        var audit = Assert.Single(audits, a => a.Id == 91);
        Assert.Equal("/Audits/Receipt/viewer-manager.png", audit.ReceiptImageUrl);
        Assert.Contains(audit.Details, d => d.ItemName == "Ink");
    }
}
```

- [ ] **Step 2: Run tests to verify red**

Run:

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --filter "FullyQualifiedName~BranchVerifyList_LoadsDetailsForFullAuditViewer|FullyQualifiedName~VerifyList_LoadsDetailsForFullAuditViewer"
```

Expected: tests fail before implementation if queue data is incomplete or tests are not yet wired.

## Task 2: Theme-aligned pending color

**Files:**
- Modify: `AuditCkDayo/Views/Shared/_Layout.cshtml`

- [ ] **Step 1: Change pending token**

Change:

```js
"audit-pending": "#9A5A00",
```

To:

```js
"audit-pending": "#3B5B92",
```

- [ ] **Step 2: Verify visual inheritance**

Affected views already use `text-audit-pending`, `bg-audit-pending/10`, and `border-audit-pending/20`, so the token change updates pending counters and badges without scattered class edits.

## Task 3: Branch full audit viewer modal

**Files:**
- Modify: `AuditCkDayo/Views/Audits/BranchVerifyList.cshtml`

- [ ] **Step 1: Add viewer button**

Add a `View Full Audit` button in the inspector above the verify/reject action row:

```html
<button type="button" id="openAuditViewerBtn" class="w-full bg-surface-container text-primary border border-primary/20 font-label-caps py-space-3 rounded-lg hover:bg-primary hover:text-on-primary transition-all flex items-center justify-center gap-space-2 text-[12px] uppercase">
    <span class="material-symbols-outlined text-[18px]">open_in_full</span>
    View Full Audit
</button>
```

- [ ] **Step 2: Add modal markup**

Add a hidden full-screen modal after the main grid:

```html
<div id="auditViewerModal" class="fixed inset-0 z-[100] hidden" role="dialog" aria-modal="true" aria-labelledby="auditViewerTitle">
    <div id="auditViewerBackdrop" class="absolute inset-0 bg-primary/80 backdrop-blur-sm"></div>
    <div class="relative z-10 h-full w-full p-space-6 flex items-center justify-center">
        <section class="bg-surface-container-lowest border border-surface-border rounded-xl shadow-2xl w-full max-w-7xl max-h-[92vh] overflow-hidden grid grid-cols-1 lg:grid-cols-[minmax(0,1.4fr)_minmax(360px,0.6fr)]">
            <div class="bg-surface-container-highest min-h-[420px] max-h-[92vh] flex items-center justify-center p-space-6 overflow-auto">
                <img id="viewer-receipt" src="" alt="Full receipt image" class="max-w-full max-h-[84vh] object-contain rounded-lg shadow-lg bg-white" />
            </div>
            <aside class="p-space-6 overflow-y-auto max-h-[92vh] space-y-space-6">
                <div class="flex items-start justify-between gap-space-4">
                    <div>
                        <span class="font-label-caps text-on-surface-variant uppercase text-[10px]">Full Audit Viewer</span>
                        <h2 id="auditViewerTitle" class="font-headline-md text-headline-md text-on-surface mt-1">Audit Details</h2>
                    </div>
                    <button type="button" id="closeAuditViewerBtn" class="bg-surface-container text-on-surface-variant hover:text-error border border-surface-border rounded-lg p-2 transition-colors" aria-label="Close audit viewer">
                        <span class="material-symbols-outlined">close</span>
                    </button>
                </div>
                <dl class="grid grid-cols-1 gap-space-4 text-[13px]">
                    <div><dt class="font-label-caps text-on-surface-variant uppercase text-[10px]">Buyer</dt><dd id="viewer-buyer" class="font-body-md text-on-surface"></dd></div>
                    <div><dt class="font-label-caps text-on-surface-variant uppercase text-[10px]">Establishment</dt><dd id="viewer-establishment" class="font-body-md text-on-surface"></dd></div>
                    <div><dt class="font-label-caps text-on-surface-variant uppercase text-[10px]">Amount</dt><dd id="viewer-amount" class="font-data-mono text-primary text-[22px] font-bold"></dd></div>
                    <div><dt class="font-label-caps text-on-surface-variant uppercase text-[10px]">Description</dt><dd id="viewer-description" class="font-body-md text-data-text"></dd></div>
                    <div><dt class="font-label-caps text-on-surface-variant uppercase text-[10px]">Notes</dt><dd id="viewer-notes" class="font-body-md text-on-surface-variant"></dd></div>
                </dl>
                <div class="border-t border-surface-border pt-space-4">
                    <h3 class="font-label-caps text-on-surface-variant uppercase text-[10px] mb-space-2">Line Items</h3>
                    <div class="overflow-x-auto border border-surface-border rounded-lg">
                        <table class="w-full text-left text-[11px]"><tbody id="viewer-items-body"></tbody></table>
                    </div>
                </div>
            </aside>
        </section>
    </div>
</div>
```

- [ ] **Step 3: Add JS functions**

Extend the existing script with `updateAuditViewerFromRow(row)`, `openAuditViewer()`, and `closeAuditViewer()`. Call `updateAuditViewerFromRow(row)` inside `selectAudit(row)`.

## Task 4: Manager full audit viewer modal

**Files:**
- Modify: `AuditCkDayo/Views/Audits/VerifyList.cshtml`

- [ ] **Step 1: Apply the same button, modal markup, and JS pattern**

Use the same IDs and logic as Task 3 because each Razor view is a separate page. The manager copy should say `Full Audit Viewer`; the existing approve/reject forms stay unchanged.

## Task 5: Verification

**Files:**
- Validate: `AuditCkDayo.Tests/UnitTest1.cs`
- Validate: browser-rendered verification pages

- [ ] **Step 1: Run targeted tests**

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release --filter "FullyQualifiedName~BranchVerifyList_LoadsDetailsForFullAuditViewer|FullyQualifiedName~VerifyList_LoadsDetailsForFullAuditViewer"
```

Expected: `Passed! - Failed: 0`.

- [ ] **Step 2: Run full tests**

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release
```

Expected: all tests pass.

- [ ] **Step 3: Browser smoke test**

Launch the app, sign in with a seeded role that can see a queue, open the verification page, click `View Full Audit`, confirm the modal opens, the receipt is not cropped, line items render, Escape closes the modal, and the page still submits existing verify/approve/reject forms.
