# Manager Coverage Task Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Activate the Manager Coverage system so that when a manager is unavailable, their pending approvals (Audits, Sales Reports, Cash Surrenders) are dynamically routed and authorized for the designated covering manager.

**Architecture:**
* Create `CoverageService.cs` - a scoped helper service that queries `ManagerCoverages` to find which managers the current user is covering for today under a given scope.
* Update `HomeController.cs` (Dashboard) - include covered managers' pending count in dashboard metrics.
* Update `AuditsController.cs` (VerifyList & SurrenderQueue) - load covered managers' pending audits and surrenders.
* Update `SalesReportsController.cs` (Index) - load covered managers' pending daily sales reports.
* Update Authorization Logic - allow covering managers with appropriate scope flags to approve/reject covered managers' items.

**Tech Stack:** ASP.NET Core MVC (.NET 9), Entity Framework Core (MySQL).

---

### Task 1: Project Registration & CoverageService Implementation

**Files:**
* Create: `AuditCkDayo/Services/CoverageService.cs`
* Modify: `AuditCkDayo/Program.cs`

- [ ] **Step 1: Create CoverageService.cs**

Create `CoverageService.cs` in the `Services` folder:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuditCkDayo.Services
{
    public class CoverageService
    {
        private readonly AuditDbContext _context;

        public CoverageService(AuditDbContext context)
        {
            _context = context;
        }

        public async Task<List<int>> GetCoveredManagerIdsAsync(int coveringManagerId, DateTime date, CoverageScope scope)
        {
            var day = date.Date;
            return await _context.ManagerCoverages
                .AsNoTracking()
                .Where(c => c.CoveringManagerId == coveringManagerId 
                    && c.IsActive 
                    && c.StartDate.Date <= day 
                    && c.EndDate.Date >= day)
                .ToListAsync()
                .ContinueWith(t => t.Result
                    .Where(c => (c.Scope & scope) != CoverageScope.None)
                    .Select(c => c.CoveredManagerId)
                    .ToList());
        }

        public List<int> GetCoveredManagerIds(int coveringManagerId, DateTime date, CoverageScope scope)
        {
            var day = date.Date;
            return _context.ManagerCoverages
                .AsNoTracking()
                .Where(c => c.CoveringManagerId == coveringManagerId 
                    && c.IsActive 
                    && c.StartDate.Date <= day 
                    && c.EndDate.Date >= day)
                .ToList()
                .Where(c => (c.Scope & scope) != CoverageScope.None)
                .Select(c => c.CoveredManagerId)
                .ToList();
        }
    }
}
```

- [ ] **Step 2: Register CoverageService in Program.cs**

Add the scoped service registration in `Program.cs`:

```csharp
builder.Services.AddScoped<AuditCkDayo.Services.CoverageService>();
```

- [ ] **Step 3: Commit**

```bash
git add AuditCkDayo/Services/CoverageService.cs AuditCkDayo/Program.cs
git commit -m "feat: add CoverageService and register in Program.cs"
```

---

### Task 2: Write Failing Unit Tests (TDD - RED Phase)

**Files:**
* Modify: `AuditCkDayo.Tests/UnitTest1.cs`

- [ ] **Step 1: Add failing test for Dashboard and Queue delegation**

Add `HomeController_Index_IncludesCoveredManagersTasksOnActiveCoverage` and `AuditsController_VerifyList_IncludesCoveredManagersAudits` to `UnitTest1.cs`. These will verify that when Manager B is covering for Manager A, Manager A's pending dashboard and verification items appear in Manager B's views.

- [ ] **Step 2: Run test and verify it fails**

Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter "FullyQualifiedName~IncludesCoveredManagers"`
Expected: FAIL.

- [ ] **Step 3: Commit**

```bash
git add AuditCkDayo.Tests/UnitTest1.cs
git commit -m "test: add failing tests for manager coverage task routing"
```

---

### Task 3: HomeController (Dashboard) Integration (GREEN Phase)

**Files:**
* Modify: `AuditCkDayo/Controllers/HomeController.cs`
* Modify: `AuditCkDayo/Views/Shared/_Layout.cshtml`

- [ ] **Step 1: Inject CoverageService into HomeController and update index queries**

Update `HomeController.cs` to resolve covered managers:
* If the user is a `Manager`, query the `GetCoveredManagerIdsAsync(userId, today, ...)` for scopes `BuyerAudits` and `SalesReports`.
* Expand `pendingSalesQuery` and audit `query` to also match `a.Buyer.ManagerId` inside the covered manager IDs list.

- [ ] **Step 2: Update _Layout.cshtml pending badges**

Inject `CoverageService` into `_Layout.cshtml` and update the badge count logic:
```csharp
@inject AuditCkDayo.Services.CoverageService coverageService
```
* Expand the counts for `pendingAuditsCount` and `pendingSurrendersCount` to include items managed by covered managers.

- [ ] **Step 3: Commit**

```bash
git add AuditCkDayo/Controllers/HomeController.cs AuditCkDayo/Views/Shared/_Layout.cshtml
git commit -m "feat: integrate manager coverage routing on dashboard and sidebar badges"
```

---

### Task 4: AuditsController (VerifyList & Surrenders) Integration

**Files:**
* Modify: `AuditCkDayo/Controllers/AuditsController.cs`

- [ ] **Step 1: Inject CoverageService and update actions**

Update `AuditsController.cs`:
* `VerifyList()`: Include audits for covered managers (scope `BuyerAudits`).
* `Verify()` POST: Allow covering managers to approve/reject.
* `SurrenderQueue()`: Include surrenders for covered managers (scope `AuditSettlement` or `BranchHandovers`).
* `ActionSurrender()` POST: Allow covering managers to confirm/reject surrenders.

- [ ] **Step 2: Commit**

```bash
git add AuditCkDayo/Controllers/AuditsController.cs
git commit -m "feat: integrate manager coverage routing in AuditsController verification and surrender actions"
```

---

### Task 5: SalesReportsController Integration

**Files:**
* Modify: `AuditCkDayo/Controllers/SalesReportsController.cs`

- [ ] **Step 1: Update SalesReportsController Index & Review Actions**

Update `SalesReportsController.cs`:
* Inject `CoverageService`.
* `Index()`: Include pending sales reports for covered managers (scope `SalesReports`).
* `CurrentUserCannotAccessAsync()`: Allow covering managers to view and review.

- [ ] **Step 2: Run all tests to verify**

Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter "FullyQualifiedName!=AuditCkDayo.Tests.UsersControllerTests+AuditsControllerTests.GoogleGeminiOcrService_IntegratesWithRealApiSuccessfully"`
Expected: PASS (All 143+ tests pass).

- [ ] **Step 3: Commit**

```bash
git add AuditCkDayo/Controllers/SalesReportsController.cs
git commit -m "feat: integrate manager coverage routing in SalesReportsController verifications"
```
