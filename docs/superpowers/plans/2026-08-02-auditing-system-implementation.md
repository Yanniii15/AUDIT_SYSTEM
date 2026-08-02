# Auditing System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a robust, scalable PCF (Petty Cash Fund) auditing web application in C# / ASP.NET Core MVC that connects to a XAMPP MySQL database and integrates receipt OCR.

**Architecture:** Clean architecture using ASP.NET Core MVC. The database is accessed via Entity Framework Core using the Pomelo MySQL driver. Controllers handle user interaction and workflow state transitions, while services wrap OCR parsing.

**Tech Stack:** C#, .NET 9, ASP.NET Core MVC, Entity Framework Core, Pomelo.EntityFrameworkCore.MySql, Azure.AI.FormRecognizer, Bootstrap.

---

### Task 1: Clean Workspace and Scaffold Project

**Files:**
- Create: `AuditCkDayo.sln`
- Create: `AuditCkDayo/AuditCkDayo.csproj`
- Create: `AuditCkDayo/Program.cs`

- [ ] **Step 1: Move Rails files to a reference folder**

Run the following command to move all existing Rails files into a `reference_rails` directory to keep our root folder clean.
Run:
```bash
mkdir reference_rails
mv app bin config db db.sqlite3 Gemfile Gemfile.lock Rakefile README.md config.ru Rakefile public test tmp lib vendor .ruby-version Dockerfile .rubocop.yml render.yml .kamal reference_rails/
```

- [ ] **Step 2: Scaffold a new ASP.NET Core MVC project**

Run:
```bash
dotnet new mvc -n AuditCkDayo
dotnet new sln -n AuditCkDayo
dotnet sln AuditCkDayo.sln add AuditCkDayo/AuditCkDayo.csproj
```

- [ ] **Step 3: Install Entity Framework Core and MySQL packages**

Run:
```bash
cd AuditCkDayo
dotnet add package Pomelo.EntityFrameworkCore.MySql
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Azure.AI.FormRecognizer
```

- [ ] **Step 4: Verify project compiles**

Run:
```bash
dotnet build
```
Expected: Build succeeds with 0 errors.

- [ ] **Step 5: Commit**

Run:
```bash
git add .
git commit -m "chore: scaffold asp.net core mvc project and install dependencies"
```

---

### Task 2: Implement Domain Models and DB Context

**Files:**
- Create: `AuditCkDayo/Models/User.cs`
- Create: `AuditCkDayo/Models/Establishment.cs`
- Create: `AuditCkDayo/Models/AuditItem.cs`
- Create: `AuditCkDayo/Models/AuditItemDetail.cs`
- Create: `AuditCkDayo/Data/AuditDbContext.cs`
- Modify: `AuditCkDayo/appsettings.json`

- [ ] **Step 1: Create User model**

Create `AuditCkDayo/Models/User.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum UserRole
    {
        Owner,
        Manager,
        Buyer
    }

    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        public int? ManagerId { get; set; }

        [ForeignKey("ManagerId")]
        public virtual User? Manager { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal PcfBalance { get; set; } = 0.00m;

        [Column(TypeName = "decimal(12,2)")]
        public decimal DailyStartingFloat { get; set; } = 0.00m;

        public virtual ICollection<User> StaffMembers { get; set; } = new List<User>();
        public virtual ICollection<AuditItem> AuditItems { get; set; } = new List<AuditItem>();
    }
}
```

- [ ] **Step 2: Create Establishment model**

Create `AuditCkDayo/Models/Establishment.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace AuditCkDayo.Models
{
    public class Establishment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public virtual ICollection<AuditItem> AuditItems { get; set; } = new List<AuditItem>();
    }
}
```

- [ ] **Step 3: Create AuditItem and Detail models**

Create `AuditCkDayo/Models/AuditItem.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public enum AuditStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class AuditItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BuyerId { get; set; }

        [ForeignKey("BuyerId")]
        public virtual User Buyer { get; set; } = null!;

        [Required]
        public int EstablishmentId { get; set; }

        [ForeignKey("EstablishmentId")]
        public virtual Establishment Establishment { get; set; } = null!;

        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime EntryDate { get; set; } = DateTime.Today;

        [Required]
        public AuditStatus Status { get; set; } = AuditStatus.Pending;

        public string? Notes { get; set; }

        [MaxLength(255)]
        public string? ReceiptImageUrl { get; set; }

        public int? VerifiedById { get; set; }

        [ForeignKey("VerifiedById")]
        public virtual User? VerifiedBy { get; set; }

        public DateTime? VerificationDate { get; set; }

        public virtual ICollection<AuditItemDetail> Details { get; set; } = new List<AuditItemDetail>();
    }
}
```

Create `AuditCkDayo/Models/AuditItemDetail.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuditCkDayo.Models
{
    public class AuditItemDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AuditItemId { get; set; }

        [ForeignKey("AuditItemId")]
        public virtual AuditItem AuditItem { get; set; } = null!;

        [Required]
        [MaxLength(150)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        public int Quantity { get; set; } = 1;

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Price { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal Total { get; set; }
    }
}
```

- [ ] **Step 4: Create AuditDbContext**

Create `AuditCkDayo/Data/AuditDbContext.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Models;

namespace AuditCkDayo.Data
{
    public class AuditDbContext : DbContext
    {
        public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Establishment> Establishments { get; set; }
        public DbSet<AuditItem> AuditItems { get; set; }
        public DbSet<AuditItemDetail> AuditItemDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Establishment>()
                .HasIndex(e => e.Name)
                .IsUnique();

            // Self-referential User relationship for Manager -> Staff
            modelBuilder.Entity<User>()
                .HasOne(u => u.Manager)
                .WithMany(u => u.StaffMembers)
                .HasForeignKey(u => u.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditItem -> Buyer relationship
            modelBuilder.Entity<AuditItem>()
                .HasOne(a => a.Buyer)
                .WithMany(u => u.AuditItems)
                .HasForeignKey(a => a.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditItem -> VerifiedBy relationship
            modelBuilder.Entity<AuditItem>()
                .HasOne(a => a.VerifiedBy)
                .WithMany()
                .HasForeignKey(a => a.VerifiedById)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
```

- [ ] **Step 5: Setup connection string in appsettings.json**

Modify `AuditCkDayo/appsettings.json` to configure MySQL connection string (pointing to local XAMPP MySQL):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=audit_ckr_dayo;user=root;password="
  }
}
```

- [ ] **Step 6: Register DbContext in Program.cs**

Modify `AuditCkDayo/Program.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

- [ ] **Step 7: Verify compilation**

Run:
```bash
dotnet build
```
Expected: Build succeeds with 0 errors.

- [ ] **Step 8: Commit**

Run:
```bash
git add .
git commit -m "feat: implement data models, db context, and mysql config"
```

---

### Task 3: Implement Authentication and Middleware

We will implement Cookie Authentication for our custom users with Role checks.

**Files:**
- Create: `AuditCkDayo/Controllers/AccountController.cs`
- Create: `AuditCkDayo/ViewModels/LoginViewModel.cs`
- Create: `AuditCkDayo/ViewModels/RegisterViewModel.cs`
- Create: `AuditCkDayo/Views/Account/Login.cshtml`
- Modify: `AuditCkDayo/Program.cs`

- [ ] **Step 1: Add Cookie Authentication to Program.cs**

Modify `AuditCkDayo/Program.cs` to add Authentication services and middleware:
Insert:
```csharp
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });
```
Make sure `app.UseAuthentication();` is called before `app.UseAuthorization();`.

- [ ] **Step 2: Create Login and Register ViewModels**

Create `AuditCkDayo/ViewModels/LoginViewModel.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace AuditCkDayo.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
```

Create `AuditCkDayo/ViewModels/RegisterViewModel.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        public int? ManagerId { get; set; }
    }
}
```

- [ ] **Step 3: Create AccountController**

Create `AuditCkDayo/Controllers/AccountController.cs` implementing Login, Logout, Register (using BCrypt/basic hashing for secure password storage):
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;

namespace AuditCkDayo.Controllers
{
    public class AccountController : Controller
    {
        private readonly AuditDbContext _context;

        public AccountController(AuditDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var managers = await _context.Users
                .Where(u => u.Role == UserRole.Manager)
                .ToListAsync();
            ViewBag.Managers = new SelectList(managers, "Id", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existing = await _context.Users.AnyAsync(u => u.Email == model.Email);
                if (existing)
                {
                    ModelState.AddModelError("Email", "Email is already taken.");
                }
                else
                {
                    var user = new User
                    {
                        Name = model.Name,
                        Email = model.Email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                        Role = model.Role,
                        ManagerId = model.Role == UserRole.Buyer ? model.ManagerId : null,
                        PcfBalance = model.Role == UserRole.Buyer ? 10000.00m : 0.00m // Default starting balance
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("Login");
                }
            }

            var managers = await _context.Users
                .Where(u => u.Role == UserRole.Manager)
                .ToListAsync();
            ViewBag.Managers = new SelectList(managers, "Id", "Name", model.ManagerId);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();
    }
}
```

*Note: Since we use BCrypt.Net-Next, we need to install the BCrypt package:*
Run:
```bash
dotnet add package BCrypt.Net-Next
```

- [ ] **Step 4: Create Login View**

Create `AuditCkDayo/Views/Account/Login.cshtml`:
```html
@model AuditCkDayo.ViewModels.LoginViewModel
@{
    ViewData["Title"] = "Login";
}

<div class="row justify-content-center mt-5">
    <div class="col-md-4">
        <h2 class="text-center">Audit System Login</h2>
        <form asp-action="Login" method="post" class="mt-4">
            <div asp-validation-summary="ModelOnly" class="text-danger"></div>
            <div class="form-group mb-3">
                <label asp-for="Email" class="form-label"></label>
                <input asp-for="Email" class="form-control" />
                <span asp-validation-for="Email" class="text-danger"></span>
            </div>
            <div class="form-group mb-3">
                <label asp-for="Password" class="form-label"></label>
                <input asp-for="Password" type="password" class="form-control" />
                <span asp-validation-for="Password" class="text-danger"></span>
            </div>
            <button type="submit" class="btn btn-primary w-100">Login</button>
        </form>
        <p class="mt-3 text-center">Don't have an account? <a asp-action="Register">Register here</a></p>
    </div>
</div>
```

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
git commit -m "feat: implement auth controllers, viewmodels, and login page"
```

---

### Task 4: Establishments Management

**Files:**
- Create: `AuditCkDayo/Controllers/EstablishmentsController.cs`
- Create: `AuditCkDayo/Views/Establishments/Index.cshtml`
- Create: `AuditCkDayo/Views/Establishments/Create.cshtml`

- [ ] **Step 1: Create EstablishmentsController**

Create `AuditCkDayo/Controllers/EstablishmentsController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Owner,Manager")]
    public class EstablishmentsController : Controller
    {
        private readonly AuditDbContext _context;

        public EstablishmentsController(AuditDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var establishments = await _context.Establishments.ToListAsync();
            return View(establishments);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(Establishment establishment)
        {
            if (ModelState.IsValid)
            {
                var exists = await _context.Establishments.AnyAsync(e => e.Name == establishment.Name);
                if (exists)
                {
                    ModelState.AddModelError("Name", "Establishment name already exists.");
                    return View(establishment);
                }

                _context.Establishments.Add(establishment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(establishment);
        }
    }
}
```

- [ ] **Step 2: Create Establishments Views**

Create `AuditCkDayo/Views/Establishments/Index.cshtml`:
```html
@model IEnumerable<AuditCkDayo.Models.Establishment>
@{
    ViewData["Title"] = "Establishments";
}

<div class="d-flex justify-content-between align-items-center my-4">
    <h2>Establishments</h2>
    <a asp-action="Create" class="btn btn-success">Add New Establishment</a>
</div>

<table class="table table-striped">
    <thead>
        <tr>
            <th>ID</th>
            <th>Name</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var item in Model)
        {
            <tr>
                <td>@item.Id</td>
                <td>@item.Name</td>
            </tr>
        }
    </tbody>
</table>
```

Create `AuditCkDayo/Views/Establishments/Create.cshtml`:
```html
@model AuditCkDayo.Models.Establishment
@{
    ViewData["Title"] = "Add Establishment";
}

<h2>Add Establishment</h2>
<hr />
<div class="row">
    <div class="col-md-4">
        <form asp-action="Create">
            <div asp-validation-summary="ModelOnly" class="text-danger"></div>
            <div class="form-group mb-3">
                <label asp-for="Name" class="form-label"></label>
                <input asp-for="Name" class="form-control" />
                <span asp-validation-for="Name" class="text-danger"></span>
            </div>
            <button type="submit" class="btn btn-primary">Create</button>
            <a asp-action="Index" class="btn btn-secondary">Back to List</a>
        </form>
    </div>
</div>
```

- [ ] **Step 3: Commit**

Run:
```bash
git add .
git commit -m "feat: implement establishments model routing and dashboard"
```

---

### Task 5: OCR Processing & Receipt Upload Flow

**Files:**
- Create: `AuditCkDayo/Services/IOcrService.cs`
- Create: `AuditCkDayo/Services/AzureOcrService.cs`
- Create: `AuditCkDayo/Controllers/AuditsController.cs`
- Create: `AuditCkDayo/ViewModels/AuditSubmissionViewModel.cs`
- Create: `AuditCkDayo/Views/Audits/Upload.cshtml`
- Create: `AuditCkDayo/Views/Audits/Review.cshtml`
- Modify: `AuditCkDayo/Program.cs`

- [ ] **Step 1: Create OCR Service Interfaces and Mock Implementation**

To enable testing without direct Azure configuration details immediately, we will design an `IOcrService` that supports Azure AI but falls back gracefully.

Create `AuditCkDayo/Services/IOcrService.cs`:
```csharp
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

namespace AuditCkDayo.Services
{
    public class OcrItemResult
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }

    public class OcrResult
    {
        public decimal TotalAmount { get; set; }
        public DateTime? TransactionDate { get; set; }
        public List<OcrItemResult> Items { get; set; } = new();
    }

    public interface IOcrService
    {
        Task<OcrResult> ParseReceiptAsync(Stream imageStream);
    }
}
```

Create `AuditCkDayo/Services/AzureOcrService.cs` wrapping Azure Form Recognizer API:
```csharp
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace AuditCkDayo.Services
{
    public class AzureOcrService : IOcrService
    {
        private readonly string _apiKey;
        private readonly string _endpoint;

        public AzureOcrService(IConfiguration configuration)
        {
            _apiKey = configuration["AzureOcr:ApiKey"] ?? "";
            _endpoint = configuration["AzureOcr:Endpoint"] ?? "";
        }

        public async Task<OcrResult> ParseReceiptAsync(Stream imageStream)
        {
            var result = new OcrResult();

            // FALLBACK / MOCK: if credentials are not configured, simulate OCR response
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_endpoint))
            {
                result.TotalAmount = 250.50m;
                result.TransactionDate = DateTime.Today;
                result.Items.Add(new OcrItemResult { Name = "Sample Item A", Quantity = 2, Price = 100.00m, Total = 200.00m });
                result.Items.Add(new OcrItemResult { Name = "Sample Item B", Quantity = 1, Price = 50.50m, Total = 50.50m });
                return result;
            }

            var credential = new AzureKeyCredential(_apiKey);
            var client = new DocumentAnalysisClient(new Uri(_endpoint), credential);

            var operation = await client.AnalyzeDocument OliviaAsync(WaitUntil.Completed, "prebuilt-receipt", imageStream);
            var analyzeResult = operation.Value;

            foreach (var document in analyzeResult.Documents)
            {
                if (document.Fields.TryGetValue("Total", out var totalField) && totalField.ValueType == DocumentFieldValueType.Double)
                {
                    result.TotalAmount = (decimal)totalField.Value.AsDouble();
                }

                if (document.Fields.TryGetValue("TransactionDate", out var dateField) && dateField.ValueType == DocumentFieldValueType.Date)
                {
                    result.TransactionDate = dateField.Value.AsDate().DateTime;
                }

                if (document.Fields.TryGetValue("Items", out var itemsField) && itemsField.ValueType == DocumentFieldValueType.List)
                {
                    foreach (var itemField in itemsField.Value.AsList())
                    {
                        var ocrItem = new OcrItemResult();
                        if (itemField.ValueType == DocumentFieldValueType.Dictionary)
                        {
                            var itemDict = itemField.Value.AsDictionary();
                            if (itemDict.TryGetValue("Description", out var descField) && descField.ValueType == DocumentFieldValueType.String)
                            {
                                ocrItem.Name = descField.Value.AsString();
                            }
                            if (itemDict.TryGetValue("Quantity", out var qtyField) && qtyField.ValueType == DocumentFieldValueType.Double)
                            {
                                ocrItem.Quantity = (int)qtyField.Value.AsDouble();
                            }
                            if (itemDict.TryGetValue("Price", out var priceField) && priceField.ValueType == DocumentFieldValueType.Double)
                            {
                                ocrItem.Price = (decimal)priceField.Value.AsDouble();
                            }
                            if (itemDict.TryGetValue("TotalPrice", out var totalPriceField) && totalPriceField.ValueType == DocumentFieldValueType.Double)
                            {
                                ocrItem.Total = (decimal)totalPriceField.Value.AsDouble();
                            }
                            else
                            {
                                ocrItem.Total = ocrItem.Price * ocrItem.Quantity;
                            }
                        }
                        result.Items.Add(ocrItem);
                    }
                }
            }

            return result;
        }
    }
}
```

- [ ] **Step 2: Add IOcrService to dependency injection**

Modify `AuditCkDayo/Program.cs`:
```csharp
builder.Services.AddScoped<AuditCkDayo.Services.IOcrService, AuditCkDayo.Services.AzureOcrService>();
```

- [ ] **Step 3: Create ViewModel for Audit Submission**

Create `AuditCkDayo/ViewModels/AuditSubmissionViewModel.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using AuditCkDayo.Services;

namespace AuditCkDayo.ViewModels
{
    public class AuditSubmissionViewModel
    {
        [Required]
        public int EstablishmentId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime EntryDate { get; set; }

        public string? Notes { get; set; }

        public string? ReceiptImageUrl { get; set; }

        public List<OcrItemResult> Items { get; set; } = new();
    }
}
```

- [ ] **Step 4: Create AuditsController**

Create `AuditCkDayo/Controllers/AuditsController.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.Services;
using AuditCkDayo.ViewModels;

namespace AuditCkDayo.Controllers
{
    [Authorize]
    public class AuditsController : Controller
    {
        private readonly AuditDbContext _context;
        private readonly IOcrService _ocrService;
        private readonly IWebHostEnvironment _env;

        public AuditsController(AuditDbContext context, IOcrService ocrService, IWebHostEnvironment env)
        {
            _context = context;
            _ocrService = ocrService;
            _env = env;
        }

        [HttpGet]
        [Authorize(Roles = "Buyer")]
        public IActionResult Upload() => View();

        [HttpPost]
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> ProcessUpload(IFormFile receipt)
        {
            if (receipt == null || receipt.Length == 0)
            {
                ModelState.AddModelError("", "Please upload a valid receipt image.");
                return View("Upload");
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(receipt.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await receipt.CopyToAsync(stream);
            }

            // Perform OCR
            using (var stream = new FileStream(filePath, FileMode.Open, FileStreamAccess.Read))
            {
                var ocrResult = await _ocrService.ParseReceiptAsync(stream);
                TempData["ReceiptImageUrl"] = "/uploads/" + fileName;
                TempData["TotalAmount"] = ocrResult.TotalAmount.ToString();
                TempData["TransactionDate"] = ocrResult.TransactionDate?.ToString("yyyy-MM-dd");
                
                // Keep serialized items in session or tempdata
                HttpContext.Session.SetString("OcrItems", System.Text.Json.JsonSerializer.Serialize(ocrResult.Items));
            }

            return RedirectToAction(nameof(Review));
        }

        [HttpGet]
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> Review()
        {
            var establishments = await _context.Establishments.ToListAsync();
            ViewBag.Establishments = new SelectList(establishments, "Id", "Name");

            var itemsJson = HttpContext.Session.GetString("OcrItems") ?? "[]";
            var items = System.Text.Json.JsonSerializer.Deserialize<List<OcrItemResult>>(itemsJson) ?? new();

            var model = new AuditSubmissionViewModel
            {
                ReceiptImageUrl = TempData["ReceiptImageUrl"] as string,
                Amount = decimal.TryParse(TempData["TotalAmount"] as string, out var amt) ? amt : 0.00m,
                EntryDate = DateTime.TryParse(TempData["TransactionDate"] as string, out var dt) ? dt : DateTime.Today,
                Items = items
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "Buyer")]
        public async Task<IActionResult> SubmitAudit(AuditSubmissionViewModel model)
        {
            var buyerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var buyer = await _context.Users.FindAsync(buyerId);

            if (buyer == null) return Challenge();

            if (buyer.PcfBalance < model.Amount)
            {
                ModelState.AddModelError("", $"Insufficient Petty Cash Fund balance. Required: ₱{model.Amount}, Available: ₱{buyer.PcfBalance}");
                var establishments = await _context.Establishments.ToListAsync();
                ViewBag.Establishments = new SelectList(establishments, "Id", "Name", model.EstablishmentId);
                return View("Review", model);
            }

            // Deduct from wallet immediately
            buyer.PcfBalance -= model.Amount;

            var auditItem = new AuditItem
            {
                BuyerId = buyerId,
                EstablishmentId = model.EstablishmentId,
                Amount = model.Amount,
                Description = model.Description,
                EntryDate = model.EntryDate,
                Notes = model.Notes,
                ReceiptImageUrl = model.ReceiptImageUrl,
                Status = AuditStatus.Pending
            };

            _context.AuditItems.Add(auditItem);
            await _context.SaveChangesAsync();

            // Save line items
            foreach (var item in model.Items)
            {
                var detail = new AuditItemDetail
                {
                    AuditItemId = auditItem.Id,
                    ItemName = item.Name,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    Total = item.Total
                };
                _context.AuditItemDetails.Add(detail);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Home");
        }
    }
}
```

*Note: Since we use Session state in controller, we must configure Session service in Program.cs:*
Modify `AuditCkDayo/Program.cs`:
```csharp
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.HttpOnly = true;
    options.IsEssential = true;
});
```
Make sure `app.UseSession();` is called before `app.UseRouting();` in `Program.cs`.

- [ ] **Step 5: Create Views**

Create `AuditCkDayo/Views/Audits/Upload.cshtml`:
```html
@{
    ViewData["Title"] = "Upload Receipt";
}

<div class="row justify-content-center mt-5">
    <div class="col-md-6">
        <h2>Upload Receipt for Auditing</h2>
        <form asp-action="ProcessUpload" method="post" enctype="multipart/form-data" class="mt-4">
            <div class="mb-3">
                <label for="receipt" class="form-label">Receipt Image</label>
                <input type="file" name="receipt" class="form-control" accept="image/*" required />
            </div>
            <button type="submit" class="btn btn-primary w-100">Upload and Process OCR</button>
        </form>
    </div>
</div>
```

Create `AuditCkDayo/Views/Audits/Review.cshtml`:
```html
@model AuditCkDayo.ViewModels.AuditSubmissionViewModel
@{
    ViewData["Title"] = "Review Extracted Audit";
}

<h2>Review Extracted Receipt Details</h2>
<div class="row mt-4">
    <div class="col-md-6">
        <img src="@Model.ReceiptImageUrl" class="img-fluid border" alt="Receipt Upload" />
    </div>
    <div class="col-md-6">
        <form asp-action="SubmitAudit" method="post">
            <div asp-validation-summary="ModelOnly" class="text-danger"></div>
            <input type="hidden" asp-for="ReceiptImageUrl" />

            <div class="mb-3">
                <label asp-for="EstablishmentId" class="form-label">Establishment</label>
                <select asp-for="EstablishmentId" class="form-select" asp-items="ViewBag.Establishments" required>
                    <option value="">-- Select Establishment --</option>
                </select>
            </div>

            <div class="mb-3">
                <label asp-for="Amount" class="form-label">Total Amount (₱)</label>
                <input asp-for="Amount" class="form-control" step="0.01" type="number" required />
            </div>

            <div class="mb-3">
                <label asp-for="Description" class="form-label">Short Description</label>
                <input asp-for="Description" class="form-control" placeholder="e.g. Kitchen supplies purchase" required />
            </div>

            <div class="mb-3">
                <label asp-for="EntryDate" class="form-label">Date</label>
                <input asp-for="EntryDate" class="form-control" type="date" required />
            </div>

            <div class="mb-3">
                <label asp-for="Notes" class="form-label">Notes</label>
                <textarea asp-for="Notes" class="form-control"></textarea>
            </div>

            <h4>Line Items Extracted</h4>
            <table class="table">
                <thead>
                    <tr>
                        <th>Item Name</th>
                        <th>Qty</th>
                        <th>Price</th>
                        <th>Total</th>
                    </tr>
                </thead>
                <tbody>
                    @for (int i = 0; i < Model.Items.Count; i++)
                    {
                        <tr>
                            <td>
                                <input asp-for="Items[i].Name" class="form-control" />
                            </td>
                            <td>
                                <input asp-for="Items[i].Quantity" class="form-control" type="number" />
                            </td>
                            <td>
                                <input asp-for="Items[i].Price" class="form-control" type="number" step="0.01" />
                            </td>
                            <td>
                                <input asp-for="Items[i].Total" class="form-control" type="number" step="0.01" />
                            </td>
                        </tr>
                    }
                </tbody>
            </table>

            <button type="submit" class="btn btn-success w-100">Submit and Audited</button>
        </form>
    </div>
</div>
```

- [ ] **Step 6: Commit**

Run:
```bash
git add .
git commit -m "feat: implement ocr service, upload flows, review layout, and session details"
```

---

### Task 6: Manager Verification Page

**Files:**
- Create: `AuditCkDayo/Views/Audits/VerifyList.cshtml`
- Modify: `AuditCkDayo/Controllers/AuditsController.cs`

- [ ] **Step 1: Implement Verification Endpoints**

Modify `AuditCkDayo/Controllers/AuditsController.cs` to add Manager list and Approval / Rejection actions:
```csharp
        [HttpGet]
        [Authorize(Roles = "Owner,Manager")]
        public async Task<IActionResult> VerifyList()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = User.FindFirstValue(ClaimTypes.Role);

            IQueryable<AuditItem> query = _context.AuditItems
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .Where(a => a.Status == AuditStatus.Pending);

            if (role == "Manager")
            {
                // Only see assigned buyers
                query = query.Where(a => a.Buyer.ManagerId == userId);
            }

            var pendingAudits = await query.ToListAsync();
            return View(pendingAudits);
        }

        [HttpPost]
        [Authorize(Roles = "Owner,Manager")]
        public async Task<IActionResult> Verify(int id, AuditStatus action)
        {
            var audit = await _context.AuditItems.Include(a => a.Buyer).FirstOrDefaultAsync(a => a.Id == id);
            if (audit == null) return NotFound();

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = User.FindFirstValue(ClaimTypes.Role);

            // Access check
            if (role == "Manager" && audit.Buyer.ManagerId != userId)
            {
                return Forbid();
            }

            audit.Status = action;
            audit.VerifiedById = userId;
            audit.VerificationDate = DateTime.Now;

            if (action == AuditStatus.Rejected)
            {
                // Refund money to buyer wallet
                audit.Buyer.PcfBalance += audit.Amount;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(VerifyList));
        }
```

- [ ] **Step 2: Create VerifyList View**

Create `AuditCkDayo/Views/Audits/VerifyList.cshtml`:
```html
@model IEnumerable<AuditCkDayo.Models.AuditItem>
@{
    ViewData["Title"] = "Pending Verifications";
}

<h2 class="my-4">Pending Audit Verifications</h2>

@if (!Model.Any())
{
    <div class="alert alert-info">No pending audits for verification.</div>
}
else
{
    <table class="table table-bordered table-striped">
        <thead>
            <tr>
                <th>Buyer</th>
                <th>Establishment</th>
                <th>Amount</th>
                <th>Description</th>
                <th>Date</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var audit in Model)
            {
                <tr>
                    <td>@audit.Buyer.Name</td>
                    <td>@audit.Establishment.Name</td>
                    <td>₱@audit.Amount.ToString("N2")</td>
                    <td>@audit.Description</td>
                    <td>@audit.EntryDate.ToShortDateString()</td>
                    <td>
                        <div class="d-flex gap-2">
                            <form asp-action="Verify" method="post">
                                <input type="hidden" name="id" value="@audit.Id" />
                                <input type="hidden" name="action" value="Approved" />
                                <button type="submit" class="btn btn-success btn-sm">Approve</button>
                            </form>
                            <form asp-action="Verify" method="post">
                                <input type="hidden" name="id" value="@audit.Id" />
                                <input type="hidden" name="action" value="Rejected" />
                                <button type="submit" class="btn btn-danger btn-sm">Reject</button>
                            </form>
                        </div>
                    </td>
                </tr>
            }
        </tbody>
    </table>
}
```

- [ ] **Step 3: Commit**

Run:
```bash
git add .
git commit -m "feat: implement manager verification interface and refund logic on reject"
```

---

### Task 7: Filtering and Dashboard Implementation

**Files:**
- Modify: `AuditCkDayo/Controllers/HomeController.cs`
- Create: `AuditCkDayo/ViewModels/DashboardViewModel.cs`
- Modify: `AuditCkDayo/Views/Home/Index.cshtml`

- [ ] **Step 1: Create DashboardViewModel**

Create `AuditCkDayo/ViewModels/DashboardViewModel.cs`:
```csharp
using System;
using System.Collections.Generic;
using AuditCkDayo.Models;

namespace AuditCkDayo.ViewModels
{
    public class DashboardViewModel
    {
        public List<AuditItem> Audits { get; set; } = new();
        public decimal TotalAmount { get; set; }
        
        // Filters
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public AuditStatus? Status { get; set; }
        public int? EstablishmentId { get; set; }
        public int? BuyerId { get; set; }
    }
}
```

- [ ] **Step 2: Update HomeController to query and filter audits**

Modify `AuditCkDayo/Controllers/HomeController.cs`:
```csharp
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;

namespace AuditCkDayo.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AuditDbContext _context;

        public HomeController(AuditDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DashboardViewModel filter)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var role = User.FindFirstValue(ClaimTypes.Role);

            IQueryable<AuditItem> query = _context.AuditItems
                .Include(a => a.Buyer)
                .Include(a => a.Establishment);

            if (role == "Manager")
            {
                // Managers see their assigned buyers
                query = query.Where(a => a.Buyer.ManagerId == userId);
            }
            else if (role == "Buyer")
            {
                // Buyers see only their own uploads
                query = query.Where(a => a.BuyerId == userId);
            }

            // Apply Filters
            if (filter.FromDate.HasValue)
            {
                query = query.Where(a => a.EntryDate >= filter.FromDate.Value);
            }
            if (filter.ToDate.HasValue)
            {
                query = query.Where(a => a.EntryDate <= filter.ToDate.Value);
            }
            if (filter.Status.HasValue)
            {
                query = query.Where(a => a.Status == filter.Status.Value);
            }
            if (filter.EstablishmentId.HasValue)
            {
                query = query.Where(a => a.EstablishmentId == filter.EstablishmentId.Value);
            }
            if (filter.BuyerId.HasValue)
            {
                query = query.Where(a => a.BuyerId == filter.BuyerId.Value);
            }

            var result = await query.OrderByDescending(a => a.EntryDate).ToListAsync();

            filter.Audits = result;
            filter.TotalAmount = result.Sum(a => a.Amount);

            // Populate view bags for select inputs
            var establishments = await _context.Establishments.ToListAsync();
            ViewBag.Establishments = new SelectList(establishments, "Id", "Name", filter.EstablishmentId);

            if (role == "Owner")
            {
                var buyers = await _context.Users.Where(u => u.Role == UserRole.Buyer).ToListAsync();
                ViewBag.Buyers = new SelectList(buyers, "Id", "Name", filter.BuyerId);
            }
            else if (role == "Manager")
            {
                var buyers = await _context.Users.Where(u => u.Role == UserRole.Buyer && u.ManagerId == userId).ToListAsync();
                ViewBag.Buyers = new SelectList(buyers, "Id", "Name", filter.BuyerId);
            }

            return View(filter);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
```

- [ ] **Step 3: Update Dashboard View**

Modify `AuditCkDayo/Views/Home/Index.cshtml`:
```html
@model AuditCkDayo.ViewModels.DashboardViewModel
@{
    ViewData["Title"] = "Dashboard";
}

<div class="row my-4">
    <div class="col-md-9">
        <h2>Audited Items Dashboard</h2>
    </div>
    <div class="col-md-3 text-end">
        <h4 class="text-primary">Total: ₱@Model.TotalAmount.ToString("N2")</h4>
    </div>
</div>

<div class="row">
    <!-- Filter Sidebar -->
    <div class="col-md-3">
        <div class="card p-3 mb-4">
            <h5>Filters</h5>
            <form method="get" asp-action="Index">
                <div class="mb-3">
                    <label class="form-label">From Date</label>
                    <input type="date" name="FromDate" value="@Model.FromDate?.ToString("yyyy-MM-dd")" class="form-control" />
                </div>
                <div class="mb-3">
                    <label class="form-label">To Date</label>
                    <input type="date" name="ToDate" value="@Model.ToDate?.ToString("yyyy-MM-dd")" class="form-control" />
                </div>
                <div class="mb-3">
                    <label class="form-label">Status</label>
                    <select name="Status" class="form-select">
                        <option value="">All</option>
                        @foreach (var val in Enum.GetValues(typeof(AuditCkDayo.Models.AuditStatus)))
                        {
                            <option value="@val" selected="@(Model.Status?.ToString() == val.ToString())">@val</option>
                        }
                    </select>
                </div>
                <div class="mb-3">
                    <label class="form-label">Establishment</label>
                    <select name="EstablishmentId" class="form-select" asp-items="ViewBag.Establishments">
                        <option value="">All</option>
                    </select>
                </div>
                @if (User.IsInRole("Owner") || User.IsInRole("Manager"))
                {
                    <div class="mb-3">
                        <label class="form-label">Buyer</label>
                        <select name="BuyerId" class="form-select" asp-items="ViewBag.Buyers">
                            <option value="">All</option>
                        </select>
                    </div>
                }
                <button type="submit" class="btn btn-primary w-100 mb-2">Apply Filters</button>
                <a asp-action="Index" class="btn btn-outline-secondary w-100">Clear</a>
            </form>
        </div>
    </div>

    <!-- Audits List -->
    <div class="col-md-9">
        @if (!Model.Audits.Any())
        {
            <div class="alert alert-info">No audited items found matching criteria.</div>
        }
        else
        {
            <div class="table-responsive">
                <table class="table table-striped table-bordered">
                    <thead>
                        <tr>
                            <th>Buyer</th>
                            <th>Establishment</th>
                            <th>Amount</th>
                            <th>Date</th>
                            <th>Status</th>
                            <th>Description</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var item in Model.Audits)
                        {
                            <tr>
                                <td>@item.Buyer.Name</td>
                                <td>@item.Establishment.Name</td>
                                <td>₱@item.Amount.ToString("N2")</td>
                                <td>@item.EntryDate.ToShortDateString()</td>
                                <td>
                                    <span class="badge @(item.Status == AuditCkDayo.Models.AuditStatus.Approved ? "bg-success" : (item.Status == AuditCkDayo.Models.AuditStatus.Rejected ? "bg-danger" : "bg-warning"))">
                                        @item.Status
                                    </span>
                                </td>
                                <td>@item.Description</td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }
    </div>
</div>
```

- [ ] **Step 4: Commit**

Run:
```bash
git add .
git commit -m "feat: implement dashboard filters, totals panel, and database query bindings"
```
