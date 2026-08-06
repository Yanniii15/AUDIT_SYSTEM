using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Controllers;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.Services;
using Xunit;

namespace AuditCkDayo.Tests
{
    public class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    public class UsersControllerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public UsersControllerTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(_connection)
                .Options;

            using (var context = new AuditDbContext(_options))
            {
                context.Database.EnsureCreated();
            }
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private async Task SeedDataAsync(AuditDbContext context)
        {
            var users = new List<User>
            {
                new User { Id = 1, Name = "Alice Owner", Email = "alice@test.com", PasswordHash = "hash", Role = UserRole.Owner, PcfBalance = 1000m, DailyStartingFloat = 1000m },
                new User { Id = 2, Name = "Bob Manager", Email = "bob@test.com", PasswordHash = "hash", Role = UserRole.Manager, PcfBalance = 500m, DailyStartingFloat = 500m },
                new User { Id = 3, Name = "Charlie Buyer", Email = "charlie@test.com", PasswordHash = "hash", Role = UserRole.Buyer, PcfBalance = 200m, DailyStartingFloat = 200m, ManagerId = 2 },
                new User { Id = 4, Name = "David Buyer", Email = "david@test.com", PasswordHash = "hash", Role = UserRole.Buyer, PcfBalance = 100m, DailyStartingFloat = 100m, ManagerId = null }
            };

            context.Users.AddRange(users);
            await context.SaveChangesAsync();
        }

        private UsersController CreateController(AuditDbContext context, int currentUserId, string currentUserRole)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()),
                new Claim(ClaimTypes.Role, currentUserRole)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = principal };
            var tempDataProvider = new FakeTempDataProvider();
            var tempData = new TempDataDictionary(httpContext, tempDataProvider);

            return new UsersController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = tempData
            };
        }

        [Fact]
        public async Task Index_OwnerRole_ReturnsAllUsersSortedByRoleAndViewBagManagers()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 1, "Owner");

                var result = await controller.Index();

                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsAssignableFrom<IEnumerable<User>>(viewResult.Model);
                var userList = new List<User>(model);

                Assert.Equal(4, userList.Count);
                // Roles are sorted: Owner (1st), Manager (2nd), Buyer (3rd & 4th)
                Assert.Equal(UserRole.Owner, userList[0].Role);
                Assert.Equal(UserRole.Manager, userList[1].Role);
                Assert.Equal(UserRole.Buyer, userList[2].Role);

                var managers = Assert.IsAssignableFrom<IEnumerable<User>>(controller.ViewBag.Managers);
                var managerList = new List<User>(managers);
                Assert.Single(managerList);
                Assert.Equal("Bob Manager", managerList[0].Name);
            }
        }

        [Fact]
        public async Task Index_ManagerRole_ReturnsOnlyAssignedBuyers()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 2, "Manager");

                var result = await controller.Index();

                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsAssignableFrom<IEnumerable<User>>(viewResult.Model);
                var userList = new List<User>(model);

                // Bob Manager (Id 2) only has Charlie Buyer (Id 3) assigned
                Assert.Single(userList);
                Assert.Equal("Charlie Buyer", userList[0].Name);
            }
        }

        [Fact]
        public async Task AddPcf_InvalidAmount_ReturnsRedirectAndError()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 1, "Owner");

                var result = await controller.AddPcf(1, 0m, "Add");

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                Assert.Equal("Please enter a valid amount.", controller.TempData["Error"]);
            }
        }

        [Fact]
        public async Task AddPcf_SelfTransfer_AddFunds_Success()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 1, "Owner");

                var result = await controller.AddPcf(1, 100m, "Add");

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                Assert.Equal("💰 Master Vault funded with ₱100!", controller.TempData["Message"]);

                var user = await context.Users.FindAsync(1);
                Assert.Equal(1100m, user.PcfBalance);
                Assert.Equal(1100m, user.DailyStartingFloat);
            }
        }

        [Fact]
        public async Task AddPcf_SelfTransfer_SubtractFunds_Success()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 1, "Owner");

                var result = await controller.AddPcf(1, 100m, "Subtract");

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                Assert.StartsWith("Successfully subtracted from", (string)controller.TempData["Message"]);

                var user = await context.Users.FindAsync(1);
                Assert.Equal(900m, user.PcfBalance);
                Assert.Equal(900m, user.DailyStartingFloat);
            }
        }

        [Fact]
        public async Task AddPcf_SelfTransfer_SubtractFunds_InsufficientFunds_FailsAndRollsback()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 1, "Owner");

                var result = await controller.AddPcf(1, 2000m, "Subtract");

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                Assert.Equal("Error: Not enough funds.", controller.TempData["Error"]);

                var user = await context.Users.FindAsync(1);
                Assert.Equal(1000m, user.PcfBalance); // Unchanged
            }
        }

        [Fact]
        public async Task AddPcf_ManagerToBuyer_AddFunds_Success()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 2, "Manager");

                // Bob Manager (Id 2, bal 500) transfers 100 to Charlie Buyer (Id 3, bal 200)
                var result = await controller.AddPcf(3, 100m, "Add");

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                Assert.StartsWith("Successfully added to", (string)controller.TempData["Message"]);

                var manager = await context.Users.FindAsync(2);
                var buyer = await context.Users.FindAsync(3);

                Assert.Equal(400m, manager.PcfBalance);
                Assert.Equal(400m, manager.DailyStartingFloat);
                Assert.Equal(300m, buyer.PcfBalance);
                Assert.Equal(300m, buyer.DailyStartingFloat);
            }
        }

        [Fact]
        public async Task AddPcf_ManagerToBuyer_AddFunds_InsufficientManagerFunds_FailsAndRollsback()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 2, "Manager");

                // Bob Manager (Id 2, bal 500) tries to transfer 600 to Charlie Buyer (Id 3, bal 200)
                var result = await controller.AddPcf(3, 600m, "Add");

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                Assert.Equal("Error: You don't have enough funds.", controller.TempData["Error"]);

                var manager = await context.Users.FindAsync(2);
                var buyer = await context.Users.FindAsync(3);

                Assert.Equal(500m, manager.PcfBalance); // Unchanged
                Assert.Equal(200m, buyer.PcfBalance);  // Unchanged
            }
        }

        [Fact]
        public async Task AddPcf_ManagerToBuyer_SubtractFunds_Success()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 2, "Manager");

                // Bob Manager (Id 2, bal 500) subtracts 50 from Charlie Buyer (Id 3, bal 200)
                var result = await controller.AddPcf(3, 50m, "Subtract");

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                Assert.StartsWith("Successfully subtracted from", (string)controller.TempData["Message"]);

                var manager = await context.Users.FindAsync(2);
                var buyer = await context.Users.FindAsync(3);

                // Manager balance increases, Buyer balance decreases
                Assert.Equal(550m, manager.PcfBalance);
                Assert.Equal(550m, manager.DailyStartingFloat);
                Assert.Equal(150m, buyer.PcfBalance);
                Assert.Equal(150m, buyer.DailyStartingFloat);
            }
        }

        [Fact]
        public async Task AddPcf_ManagerToBuyer_SubtractFunds_InsufficientBuyerFunds_FailsAndRollsback()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 2, "Manager");

                // Bob Manager (Id 2, bal 500) tries to subtract 300 from Charlie Buyer (Id 3, bal 200)
                var result = await controller.AddPcf(3, 300m, "Subtract");

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirectResult.ActionName);
                Assert.Equal("Error: Not enough funds.", controller.TempData["Error"]);

                var manager = await context.Users.FindAsync(2);
                var buyer = await context.Users.FindAsync(3);

                Assert.Equal(500m, manager.PcfBalance); // Unchanged
                Assert.Equal(200m, buyer.PcfBalance);  // Unchanged
            }
        }

        [Fact]
        public async Task AssignManager_Success()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 1, "Owner");

                // Owner assigns Bob Manager (Id 2) to David Buyer (Id 4)
                var result = await controller.AssignManager(4, 2);

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Register", redirectResult.ActionName);
                Assert.Equal("Successfully updated manager for david@test.com.", controller.TempData["Message"]);

                var buyer = await context.Users.FindAsync(4);
                Assert.Equal(2, buyer.ManagerId);
            }
        }

        [Fact]
        public async Task AssignManager_ClearManager_Success()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 1, "Owner");

                // Owner clears manager for Charlie Buyer (Id 3)
                var result = await controller.AssignManager(3, null);

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Register", redirectResult.ActionName);

                var buyer = await context.Users.FindAsync(3);
                Assert.Null(buyer.ManagerId);
            }
        }

    public class FakeOcrService : IOcrService
    {
        public Task<OcrResult> ParseReceiptAsync(List<Stream> receiptStreams)
        {
            return Task.FromResult(new OcrResult
            {
                TotalAmount = 100.00m,
                TransactionDate = DateTime.Today,
                Items = new List<OcrItemResult>()
            });
        }
    }

    public class FakeWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "AuditCkDayo";
        public string EnvironmentName { get; set; } = "Development";
    }

    public class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _sessionStorage = new();

        public bool IsAvailable => true;
        public string Id => "FakeSessionId";
        public IEnumerable<string> Keys => _sessionStorage.Keys;

        public void Clear() => _sessionStorage.Clear();
        public Task CommitAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _sessionStorage.Remove(key);
        public void Set(string key, byte[] value) => _sessionStorage[key] = value;
        public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => _sessionStorage.TryGetValue(key, out value);
    }

    public class FakeUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new ActionContext();

        public string? Action(UrlActionContext actionContext)
        {
            return $"/{actionContext.Controller}/{actionContext.Action}";
        }

        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => routeName;
        public string? RouteUrl(UrlRouteContext routeContext) => routeContext.RouteName;
    }


    public class AuditsControllerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public AuditsControllerTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(_connection)
                .Options;

            using (var context = new AuditDbContext(_options))
            {
                context.Database.EnsureCreated();
            }
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private async Task SeedDataAsync(AuditDbContext context)
        {
            var users = new List<User>
            {
                new User { Id = 1, Name = "Alice Owner", Email = "alice@test.com", PasswordHash = "hash", Role = UserRole.Owner, PcfBalance = 1000m, DailyStartingFloat = 1000m },
                new User { Id = 2, Name = "Bob Manager", Email = "bob@test.com", PasswordHash = "hash", Role = UserRole.Manager, PcfBalance = 500m, DailyStartingFloat = 500m },
                new User { Id = 3, Name = "Charlie Buyer", Email = "charlie@test.com", PasswordHash = "hash", Role = UserRole.Buyer, PcfBalance = 200m, DailyStartingFloat = 200m, ManagerId = 2 },
                new User { Id = 4, Name = "David Buyer", Email = "david@test.com", PasswordHash = "hash", Role = UserRole.Buyer, PcfBalance = 100m, DailyStartingFloat = 100m, ManagerId = null },
                new User { Id = 5, Name = "Eve Staff", Email = "eve@test.com", PasswordHash = "hash", Role = UserRole.BranchStaff, EstablishmentId = 1 },
                new User { Id = 6, Name = "Frank OtherStaff", Email = "frank@test.com", PasswordHash = "hash", Role = UserRole.BranchStaff, EstablishmentId = 2 }
            };

            context.Users.AddRange(users);

            var establishment1 = new Establishment { Id = 1, Name = "Test Establishment 1" };
            var establishment2 = new Establishment { Id = 2, Name = "Test Establishment 2" };
            context.Establishments.AddRange(establishment1, establishment2);
            await context.SaveChangesAsync();
        }

        private AuditsController CreateController(AuditDbContext context, int currentUserId, string currentUserRole, ISession? session = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()),
                new Claim(ClaimTypes.Role, currentUserRole)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = principal };
            httpContext.Session = session ?? new FakeSession();

            var tempDataProvider = new FakeTempDataProvider();
            var tempData = new TempDataDictionary(httpContext, tempDataProvider);

            return new AuditsController(context, new FakeOcrService(), new FakeWebHostEnvironment())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = tempData,
                Url = new FakeUrlHelper()
            };
        }

        [Fact]
        public async Task SubmitAudit_CreatesBranchQueueItemAndNotifiesOnlyAssignedBranchStaff()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 3, "Buyer");
                var model = new AuditCkDayo.ViewModels.AuditSubmissionViewModel
                {
                    EstablishmentId = 1,
                    Amount = 25m,
                    Description = "Branch delivery test",
                    EntryDate = DateTime.Today,
                    ReceiptImageUrl = "/Audits/Receipt/branch-delivery.png",
                    ReceiptImageUrls = new List<string> { "/Audits/Receipt/branch-delivery.png" },
                    Items = new List<OcrItemResult>
                    {
                        new OcrItemResult { Name = "Item", Quantity = 1, Price = 25m, Total = 25m }
                    }
                };

                var submitResult = await controller.SubmitAudit(model);

                var redirectResult = Assert.IsType<RedirectToActionResult>(submitResult);
                Assert.Equal("Index", redirectResult.ActionName);

                var branchController = CreateController(context, 5, "BranchStaff");
                var branchResult = await branchController.BranchVerifyList();
                var branchView = Assert.IsType<ViewResult>(branchResult);
                var branchAudits = Assert.IsAssignableFrom<IEnumerable<AuditItem>>(branchView.Model);
                var branchAudit = Assert.Single(branchAudits);
                Assert.Equal(1, branchAudit.EstablishmentId);
                Assert.Equal(AuditStatus.AwaitingBranchVerification, branchAudit.Status);

                var otherBranchController = CreateController(context, 6, "BranchStaff");
                var otherBranchResult = await otherBranchController.BranchVerifyList();
                var otherBranchView = Assert.IsType<ViewResult>(otherBranchResult);
                var otherBranchAudits = Assert.IsAssignableFrom<IEnumerable<AuditItem>>(otherBranchView.Model);
                Assert.Empty(otherBranchAudits);

                var assignedBranchNotification = await context.Notifications.SingleAsync(n => n.UserId == 5);
                Assert.Equal("Audit Awaiting Branch Verification", assignedBranchNotification.Title);
                Assert.Equal("/Audits/BranchVerifyList", assignedBranchNotification.LinkUrl);
                Assert.False(await context.Notifications.AnyAsync(n => n.UserId == 6));
                Assert.False(await context.Notifications.AnyAsync(n => n.UserId == 2 && n.Category == "AuditSubmit"));
            }
        }

        [Fact]
        public async Task BranchVerifyList_RepairsBlankSubmittedStatusAndShowsAuditForAssignedBranch()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO AuditItems (Id, BuyerId, EstablishmentId, Amount, Description, EntryDate, Status, ReceiptImageUrl) VALUES (50, 3, 1, 46.42, 'Blank status receipt', '2026-08-06', '', '/Audits/Receipt/blank-status.jpg')");

                var controller = CreateController(context, 5, "BranchStaff");

                var result = await controller.BranchVerifyList();

                var viewResult = Assert.IsType<ViewResult>(result);
                var audits = Assert.IsAssignableFrom<IEnumerable<AuditItem>>(viewResult.Model);
                var audit = Assert.Single(audits);
                Assert.Equal(50, audit.Id);
                Assert.Equal(AuditStatus.AwaitingBranchVerification, audit.Status);
            }
        }

        [Fact]
        public void Receipt_ActionDeclaresFilenameRouteSegment()
        {
            var httpGet = typeof(AuditsController)
                .GetMethod(nameof(AuditsController.Receipt))!
                .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false);
            var route = Assert.Single(httpGet);
            Assert.Equal("Audits/Receipt/{filename}", ((HttpGetAttribute)route).Template);
        }

        [Fact]
        public async Task Receipt_FileNotFound_ReturnsNotFound()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 1, "Owner");

                var result = await controller.Receipt("nonexistent_file.png");

                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Fact]
        public async Task Receipt_AuthorizedOwner_ReturnsFile()
        {
            var filename = "test_receipt_owner.png";
            var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
            Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, filename);
            var expectedBytes = new byte[] { 1, 2, 3, 4 };
            await File.WriteAllBytesAsync(filePath, expectedBytes);

            try
            {
                using (var context = new AuditDbContext(_options))
                {
                    await SeedDataAsync(context);

                    // Add an audit item
                    var audit = new AuditItem
                    {
                        Id = 1,
                        BuyerId = 3,
                        EstablishmentId = 1,
                        Amount = 50m,
                        Description = "Test",
                        ReceiptImageUrl = $"/Audits/Receipt/{filename}"
                    };
                    context.AuditItems.Add(audit);
                    await context.SaveChangesAsync();

                    var controller = CreateController(context, 1, "Owner"); // Owner requesting
                    var result = await controller.Receipt(filename);

                    var fileResult = Assert.IsType<FileContentResult>(result);
                    Assert.Equal("image/png", fileResult.ContentType);
                    Assert.Equal(expectedBytes, fileResult.FileContents);
                }
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Fact]
        public async Task Receipt_AuthorizedBuyer_ReturnsFile()
        {
            var filename = "test_receipt_buyer.png";
            var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
            Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, filename);
            var expectedBytes = new byte[] { 5, 6, 7 };
            await File.WriteAllBytesAsync(filePath, expectedBytes);

            try
            {
                using (var context = new AuditDbContext(_options))
                {
                    await SeedDataAsync(context);

                    // Add an audit item for Buyer Id = 3
                    var audit = new AuditItem
                    {
                        Id = 2,
                        BuyerId = 3,
                        EstablishmentId = 1,
                        Amount = 50m,
                        Description = "Test",
                        ReceiptImageUrl = $"/Audits/Receipt/{filename}"
                    };
                    context.AuditItems.Add(audit);
                    await context.SaveChangesAsync();

                    var controller = CreateController(context, 3, "Buyer"); // Charlie Buyer requesting
                    var result = await controller.Receipt(filename);

                    var fileResult = Assert.IsType<FileContentResult>(result);
                    Assert.Equal("image/png", fileResult.ContentType);
                    Assert.Equal(expectedBytes, fileResult.FileContents);
                }
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Fact]
        public async Task Receipt_UnauthorizedBuyer_ReturnsForbid()
        {
            var filename = "test_receipt_unauth.png";
            var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
            Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, filename);
            await File.WriteAllBytesAsync(filePath, new byte[] { 1 });

            try
            {
                using (var context = new AuditDbContext(_options))
                {
                    await SeedDataAsync(context);

                    // Add an audit item for Buyer Id = 3
                    var audit = new AuditItem
                    {
                        Id = 3,
                        BuyerId = 3,
                        EstablishmentId = 1,
                        Amount = 50m,
                        Description = "Test",
                        ReceiptImageUrl = $"/Audits/Receipt/{filename}"
                    };
                    context.AuditItems.Add(audit);
                    await context.SaveChangesAsync();

                    var controller = CreateController(context, 4, "Buyer"); // David Buyer requesting (not the owner/buyer/manager)
                    var result = await controller.Receipt(filename);

                    Assert.IsType<ForbidResult>(result);
                }
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Fact]
        public async Task Receipt_AuthorizedSessionBuyer_ReturnsFile()
        {
            var filename = "test_receipt_session.png";
            var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
            Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, filename);
            var expectedBytes = new byte[] { 9, 10, 11 };
            await File.WriteAllBytesAsync(filePath, expectedBytes);

            try
            {
                using (var context = new AuditDbContext(_options))
                {
                    await SeedDataAsync(context);

                    var session = new FakeSession();
                    session.Set("ReceiptImageUrl", System.Text.Encoding.UTF8.GetBytes($"/Audits/Receipt/{filename}"));

                    var controller = CreateController(context, 3, "Buyer", session); // Charlie Buyer requesting
                    var result = await controller.Receipt(filename);

                    var fileResult = Assert.IsType<FileContentResult>(result);
                    Assert.Equal("image/png", fileResult.ContentType);
                    Assert.Equal(expectedBytes, fileResult.FileContents);
                }
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Fact]
        public async Task Receipt_AuthorizedBranchStaff_ReturnsFile()
        {
            var filename = "test_receipt_branchstaff.png";
            var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
            Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, filename);
            var expectedBytes = new byte[] { 12, 13, 14 };
            await File.WriteAllBytesAsync(filePath, expectedBytes);

            try
            {
                using (var context = new AuditDbContext(_options))
                {
                    await SeedDataAsync(context);

                    // Add an audit item for Establishment Id = 1
                    var audit = new AuditItem
                    {
                        Id = 10,
                        BuyerId = 3,
                        EstablishmentId = 1,
                        Amount = 50m,
                        Description = "Test",
                        ReceiptImageUrl = $"/Audits/Receipt/{filename}"
                    };
                    context.AuditItems.Add(audit);
                    await context.SaveChangesAsync();

                    var controller = CreateController(context, 5, "BranchStaff"); // Eve (Staff at Est 1) requesting
                    var result = await controller.Receipt(filename);

                    var fileResult = Assert.IsType<FileContentResult>(result);
                    Assert.Equal("image/png", fileResult.ContentType);
                    Assert.Equal(expectedBytes, fileResult.FileContents);
                }
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Fact]
        public async Task Receipt_UnauthorizedBranchStaff_ReturnsForbid()
        {
            var filename = "test_receipt_branchstaff_unauth.png";
            var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
            Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, filename);
            await File.WriteAllBytesAsync(filePath, new byte[] { 1 });

            try
            {
                using (var context = new AuditDbContext(_options))
                {
                    await SeedDataAsync(context);

                    // Add an audit item for Establishment Id = 1
                    var audit = new AuditItem
                    {
                        Id = 11,
                        BuyerId = 3,
                        EstablishmentId = 1,
                        Amount = 50m,
                        Description = "Test",
                        ReceiptImageUrl = $"/Audits/Receipt/{filename}"
                    };
                    context.AuditItems.Add(audit);
                    await context.SaveChangesAsync();

                    var controller = CreateController(context, 6, "BranchStaff"); // Frank (Staff at Est 2) requesting (different establishment)
                    var result = await controller.Receipt(filename);

                    Assert.IsType<ForbidResult>(result);
                }
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Fact]
        public async Task Receipt_PathTraversal_SanitizesFilename()
        {
            var filename = "test_receipt_traversal.png";
            var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
            Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, filename);
            var expectedBytes = new byte[] { 15, 16, 17 };
            await File.WriteAllBytesAsync(filePath, expectedBytes);

            try
            {
                using (var context = new AuditDbContext(_options))
                {
                    await SeedDataAsync(context);

                    // Add an audit item
                    var audit = new AuditItem
                    {
                        Id = 12,
                        BuyerId = 3,
                        EstablishmentId = 1,
                        Amount = 50m,
                        Description = "Test",
                        ReceiptImageUrl = $"/Audits/Receipt/{filename}"
                    };
                    context.AuditItems.Add(audit);
                    await context.SaveChangesAsync();

                    var controller = CreateController(context, 1, "Owner");
                    // Attempt path traversal path
                    var traversalFilename = "../../../" + filename;
                    var result = await controller.Receipt(traversalFilename);

                    // It should sanitize the filename and correctly serve the file as it extracts only the filename
                    var fileResult = Assert.IsType<FileContentResult>(result);
                    Assert.Equal("image/png", fileResult.ContentType);
                    Assert.Equal(expectedBytes, fileResult.FileContents);
                }
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
    }
}
