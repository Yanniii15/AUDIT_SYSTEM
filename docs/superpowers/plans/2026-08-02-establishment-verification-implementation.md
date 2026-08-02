# Establishment Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate an intermediate branch/establishment verification step into the auditing pipeline.

**Architecture:** Model and enum extensions inside our database schema, dynamic form registration logic, and sequential status changes handled via controller actions and views.

**Tech Stack:** C#, .NET 9, ASP.NET Core MVC, Entity Framework Core, Pomelo MySQL, Bootstrap.

---

### Task 1: Update Models and Run DB Migrations

**Files:**
- Modify: `AuditCkDayo/Models/User.cs`
- Modify: `AuditCkDayo/Models/AuditItem.cs`
- Modify: `AuditCkDayo/Data/AuditDbContext.cs`

- [ ] **Step 1: Update User.cs with BranchStaff role and EstablishmentId**

Modify `AuditCkDayo/Models/User.cs` to add `BranchStaff` to `UserRole` and a nullable `EstablishmentId`:
```csharp
    public enum UserRole
    {
        Owner,
        Manager,
        Buyer,
        BranchStaff
    }
```
And add properties:
```csharp
        public int? EstablishmentId { get; set; }

        [ForeignKey("EstablishmentId")]
        public virtual Establishment? Establishment { get; set; }
```

- [ ] **Step 2: Update AuditItem.cs with new AuditStatus enum states**

Modify `AuditCkDayo/Models/AuditItem.cs`:
```csharp
    public enum AuditStatus
    {
        AwaitingBranchVerification,
        AwaitingManagerApproval,
        Approved,
        Rejected
    }
```
Ensure default is `AuditStatus.AwaitingBranchVerification` (or map status names in views and actions).

- [ ] **Step 3: Update Fluent API relationships in AuditDbContext.cs**

Modify `AuditCkDayo/Data/AuditDbContext.cs` to define `User -> Establishment` relationship inside `OnModelCreating`:
```csharp
            // User -> Establishment relationship
            modelBuilder.Entity<User>()
                .HasOne(u => u.Establishment)
                .WithMany()
                .HasForeignKey(u => u.EstablishmentId)
                .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 4: Create and apply DB migration**

Run commands in terminal:
```bash
dotnet ef migrations add AddBranchVerificationRoles -p AuditCkDayo
```
*(Note: Program.cs will automatically apply the migration on startup when we run the app, but running this command generates the migration files).*

- [ ] **Step 5: Verify build**

Run:
```bash
dotnet build
```
Expected: Build succeeds.

- [ ] **Step 6: Commit**

Run:
```bash
git add .
git commit -m "feat: add BranchStaff role, EstablishmentId key, and updated AuditStatus states"
```

---

### Task 2: Update Registration View and Controller Save Logic

**Files:**
- Modify: `AuditCkDayo/Controllers/AccountController.cs`
- Modify: `AuditCkDayo/ViewModels/RegisterViewModel.cs`
- Modify: `AuditCkDayo/Views/Account/Register.cshtml`

- [ ] **Step 1: Update RegisterViewModel**

Modify `AuditCkDayo/ViewModels/RegisterViewModel.cs` to add `EstablishmentId`:
```csharp
        public int? EstablishmentId { get; set; }
```

- [ ] **Step 2: Update AccountController Registration Action**

Modify `Register` POST action in `AuditCkDayo/Controllers/AccountController.cs` to save `EstablishmentId` if `Role == UserRole.BranchStaff`:
```csharp
                    var user = new User
                    {
                        Name = model.Name,
                        Email = model.Email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                        Role = model.Role,
                        ManagerId = model.Role == UserRole.Buyer ? model.ManagerId : null,
                        EstablishmentId = model.Role == UserRole.BranchStaff ? model.EstablishmentId : null,
                        PcfBalance = 0.00m,
                        DailyStartingFloat = 0.00m
                    };
```
Also inside `PopulateRegistrationStats()` (both GET and POST fallback), populate establishments for the view dropdown:
```csharp
            ViewBag.Establishments = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(await _context.Establishments.AsNoTracking().ToListAsync(), "Id", "Name");
```

- [ ] **Step 3: Update Register View UI**

Modify `AuditCkDayo/Views/Account/Register.cshtml` to add the conditional Branch Selection dropdown:
```html
            <!-- Establishment (Conditional, only shown for BranchStaff) -->
            <div class="flex flex-col gap-space-2 opacity-30 pointer-events-none transition-all duration-300" id="branchField">
                <label asp-for="EstablishmentId" class="font-label-caps text-on-surface-variant uppercase text-[10px] tracking-wider font-semibold">Assigned Branch / Location</label>
                <select asp-for="EstablishmentId" class="w-full bg-surface-container-low border border-surface-border rounded-lg px-space-4 py-space-3 font-body-md focus:outline-none focus:ring-2 focus:ring-primary/10 focus:border-primary transition-all appearance-none cursor-pointer text-on-surface" asp-items="ViewBag.Establishments">
                    <option value="">Select branch location...</option>
                </select>
                <span asp-validation-for="EstablishmentId" class="text-audit-error text-[11px]"></span>
            </div>
```
Update JavaScript at the bottom of `Register.cshtml` to toggle `branchField` visibility when `BranchStaff` is selected in `roleSelect`:
```javascript
        const updateManagerVisibility = () => {
            if (roleSelect.value === "Buyer") {
                managerField.classList.remove("opacity-30", "pointer-events-none");
                document.getElementById("branchField").classList.add("opacity-30", "pointer-events-none");
                document.getElementById("branchField").querySelector("select").value = "";
            } else if (roleSelect.value === "BranchStaff") {
                document.getElementById("branchField").classList.remove("opacity-30", "pointer-events-none");
                managerField.classList.add("opacity-30", "pointer-events-none");
                managerField.querySelector("select").value = "";
            } else {
                managerField.classList.add("opacity-30", "pointer-events-none");
                managerField.querySelector("select").value = "";
                document.getElementById("branchField").classList.add("opacity-30", "pointer-events-none");
                document.getElementById("branchField").querySelector("select").value = "";
            }
        };
```

- [ ] **Step 4: Commit**

Run:
```bash
git add .
git commit -m "feat: implement branch assignment in registration controller and view form"
```

---

### Task 3: Implement Branch Verification Actions and View

**Files:**
- Modify: `AuditCkDayo/Controllers/AuditsController.cs`
- Create: `AuditCkDayo/Views/Audits/BranchVerifyList.cshtml`

- [ ] **Step 1: Update SubmitAudit status in AuditsController**

Ensure `SubmitAudit` sets `Status` to `AuditStatus.AwaitingBranchVerification`:
```csharp
                Status = AuditStatus.AwaitingBranchVerification
```

- [ ] **Step 2: Add Branch Verification Endpoints to AuditsController**

Create actions:
```csharp
        [HttpGet]
        [Authorize(Roles = "BranchStaff")]
        public async Task<IActionResult> BranchVerifyList()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            
            if (currentUser == null || !currentUser.EstablishmentId.HasValue) return Challenge();

            var pendingAudits = await _context.AuditItems
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .AsNoTracking()
                .Where(a => a.Status == AuditStatus.AwaitingBranchVerification && a.EstablishmentId == currentUser.EstablishmentId.Value)
                .ToListAsync();

            return View(pendingAudits);
        }

        [HttpPost]
        [Authorize(Roles = "BranchStaff")]
        public async Task<IActionResult> BranchVerify(int id, string actionType)
        {
            var audit = await _context.AuditItems.Include(a => a.Buyer).FirstOrDefaultAsync(a => a.Id == id);
            if (audit == null) return NotFound();

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var currentUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

            if (currentUser == null || audit.EstablishmentId != currentUser.EstablishmentId) return Forbid();
            if (audit.Status != AuditStatus.AwaitingBranchVerification) return BadRequest("This item is not awaiting branch verification.");

            if (actionType == "Verify")
            {
                audit.Status = AuditStatus.AwaitingManagerApproval;
            }
            else if (actionType == "Reject")
            {
                audit.Status = AuditStatus.Rejected;
                // Refund immediately
                audit.Buyer.PcfBalance += audit.Amount;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(BranchVerifyList));
        }
```

- [ ] **Step 3: Create BranchVerifyList View**

Create `AuditCkDayo/Views/Audits/BranchVerifyList.cshtml` matching the visual split-screen inspector theme:
- Left lists audits in `AwaitingBranchVerification` for their establishment.
- Right lists details, picture preview, and action buttons (`Verify Received` and `Reject`).
- Form links to POST `/Audits/BranchVerify`.

- [ ] **Step 4: Update Manager VerifyList query**

Modify `VerifyList` in `AuditsController.cs` so it only fetches audits where `Status == AuditStatus.AwaitingManagerApproval`:
```csharp
                .Where(a => a.Status == AuditStatus.AwaitingManagerApproval);
```
Ensure `Verify` action handles verification state correctly.

- [ ] **Step 5: Commit**

Run:
```bash
git add .
git commit -m "feat: implement branch verification endpoints, split list view, and manager queue updates"
```

---

### Task 4: Integrate Navbar and Update Home Dashboard

**Files:**
- Modify: `AuditCkDayo/Views/Shared/_Layout.cshtml`
- Modify: `AuditCkDayo/Controllers/HomeController.cs`

- [ ] **Step 1: Add BranchVerifyList link to Sidebar Navbar**

Modify `_Layout.cshtml` to render `Verify Deliveries` link for `BranchStaff` users:
```html
                @if (User.IsInRole("BranchStaff"))
                {
                    <a class="flex items-center px-space-4 py-space-3 rounded-xl transition-all group hover:bg-on-primary/5 text-primary-fixed-dim/70 hover:text-on-primary" 
                       asp-controller="Audits" asp-action="BranchVerifyList">
                        <span class="material-symbols-outlined mr-space-3">local_shipping</span>
                        <span class="font-label-caps uppercase">Verify Deliveries</span>
                    </a>
                }
```

- [ ] **Step 2: Update HomeController Index query**

Modify `HomeController.cs` to filter properly for `BranchStaff` (they should only see audits for their `EstablishmentId`):
```csharp
            else if (role == "BranchStaff")
            {
                query = query.Where(a => a.EstablishmentId == currentUser.EstablishmentId);
            }
```

- [ ] **Step 3: Update Dashboard Status Select Options**

In `HomeController.cs`, ensure `ViewBag.Statuses` is populated with the updated statuses.

- [ ] **Step 4: Verify build and compile**

Run:
```bash
dotnet build
```

- [ ] **Step 5: Commit**

Run:
```bash
git add .
git commit -m "feat: integrate branch staff sidebar links and dashboard filters"
```
