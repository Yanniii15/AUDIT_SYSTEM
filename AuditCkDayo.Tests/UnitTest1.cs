using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Controllers;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.Services;
using AuditCkDayo.ViewModels;
using Xunit;

namespace AuditCkDayo.Tests
{
    public class MockConfiguration : Microsoft.Extensions.Configuration.IConfiguration
    {
        private readonly string _apiKey;
        public MockConfiguration(string apiKey) { _apiKey = apiKey; }
        public string? this[string key]
        {
            get => _apiKey;
            set { }
        }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() => throw new NotImplementedException();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) => throw new NotImplementedException();
    }

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
                Assert.Equal("Master Vault funded with ₱100!", controller.TempData["Message"]);

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

        public Task<SalesReportOcrResult> ParseSalesReportAsync(Stream imageStream)
        {
            return Task.FromResult(new SalesReportOcrResult
            {
                BusinessDate = DateTime.Today,
                GrossSales = 100.00m,
                ConfirmedCashToHandover = 100.00m,
                RawJson = "{}"
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
        public async Task Receipt_AuthorizedSessionManager_ReturnsFile()
        {
            var filename = "test_receipt_session_manager.png";
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

                    var session = new FakeSession();
                    session.Set("ReceiptImageUrl", System.Text.Encoding.UTF8.GetBytes($"/Audits/Receipt/{filename}"));

                    var controller = CreateController(context, 2, "Manager", session); // Bob Manager reviewing a just-uploaded receipt
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

        [Fact]
        public async Task SubmitAudit_WithPastTransactionDateStillAppearsInAuditsToday()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var auditController = CreateController(context, 3, "Buyer");
                var model = new AuditCkDayo.ViewModels.AuditSubmissionViewModel
                {
                    EstablishmentId = 1,
                    Amount = 25m,
                    Description = "Submitted today with old receipt date",
                    EntryDate = DateTime.Today.AddDays(-14),
                    ReceiptImageUrl = "/Audits/Receipt/old-receipt-date.png",
                    ReceiptImageUrls = new List<string> { "/Audits/Receipt/old-receipt-date.png" },
                    Items = new List<OcrItemResult>
                    {
                        new OcrItemResult { Name = "Item", Quantity = 1, Price = 25m, Total = 25m }
                    }
                };

                var submitResult = await auditController.SubmitAudit(model);
                Assert.IsType<RedirectToActionResult>(submitResult);

                var homeController = new HomeController(NullLogger<HomeController>.Instance, context)
                {
                    ControllerContext = auditController.ControllerContext
                };

                var dashboardResult = await homeController.Index(new AuditCkDayo.ViewModels.DashboardViewModel());
                var viewResult = Assert.IsType<ViewResult>(dashboardResult);
                var dashboard = Assert.IsType<AuditCkDayo.ViewModels.DashboardViewModel>(viewResult.Model);

                Assert.Contains(dashboard.TodayAudits, a => a.Description == "Submitted today with old receipt date");
            }
        }

        [Fact]
        public void BranchVerifyList_ViewContainsFullAuditModalControls()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "Audits", "BranchVerifyList.cshtml"));
            var view = File.ReadAllText(viewPath);

            Assert.Contains("View Full Audit", view);
            Assert.Contains("id=\"auditViewerModal\"", view);
            Assert.Contains("id=\"viewer-receipt\"", view);
            Assert.Contains("object-contain", view);
            Assert.Contains("id=\"viewer-receipt-link\"", view);
            Assert.Contains("id=\"viewer-receipt-error\"", view);
            Assert.Contains("Open Receipt Image", view);
        }

        [Fact]
        public void VerifyList_ViewContainsFullAuditModalControls()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "Audits", "VerifyList.cshtml"));
            var view = File.ReadAllText(viewPath);

            Assert.Contains("View Full Audit", view);
            Assert.Contains("id=\"auditViewerModal\"", view);
            Assert.Contains("id=\"viewer-receipt\"", view);
            Assert.Contains("object-contain", view);
            Assert.Contains("id=\"viewer-receipt-link\"", view);
            Assert.Contains("id=\"viewer-receipt-error\"", view);
            Assert.Contains("Open Receipt Image", view);
        }

        [Fact]
        public void Review_ViewContainsResponsiveControlsAndLightbox()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "Audits", "Review.cshtml"));
            var view = File.ReadAllText(viewPath);

            Assert.Contains("id=\"lightboxModal\"", view);
            Assert.Contains("id=\"lightbox-img\"", view);
            Assert.Contains("id=\"claimed-amount-display\"", view);
            Assert.Contains("block lg:table", view);
            Assert.Contains("hidden lg:table-header-group", view);
            Assert.Contains("block lg:table-row-group", view);
        }

        [Fact]
        public async Task GoogleGeminiOcrService_IntegratesWithRealApiSuccessfully()
        {
            // Load API key from user secrets or environment
            var secretsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", "80c0c2b3-f92a-423d-bc70-0da0f3653d1c", "secrets.json");
            string apiKey = "";
            if (File.Exists(secretsPath))
            {
                var content = File.ReadAllText(secretsPath);
                using (var doc = JsonDocument.Parse(content))
                {
                    if (doc.RootElement.TryGetProperty("GoogleGemini:ApiKey", out var prop))
                    {
                        apiKey = prop.GetString() ?? "";
                    }
                }
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                return; // Skip if no API key configured
            }

            var mockConfig = new MockConfiguration(apiKey);
            var service = new GoogleGeminiOcrService(mockConfig);
            var mockImageBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");
            
            using (var stream = new MemoryStream(mockImageBytes))
            {
                var ex = await Record.ExceptionAsync(() => service.ParseReceiptAsync(new List<Stream> { stream }));
                Assert.Null(ex);
            }
        }
    }

    public class ClassificationModelTests
    {
        [Fact]
        public void Establishment_OperatingBranch_DefaultsToTrueForRealBranch()
        {
            var establishment = new Establishment { Name = "CKR Branch 5", IsOperatingBranch = true };

            Assert.True(establishment.IsOperatingBranch);
            Assert.False(establishment.IsMiscellaneous);
        }

        [Fact]
        public void CostCenter_RepresentsNonBranchClassification()
        {
            var costCenter = new CostCenter
            {
                Name = "Utilities",
                Category = CostCenterCategory.Utilities,
                IsActive = true
            };

            Assert.Equal("Utilities", costCenter.Name);
            Assert.Equal(CostCenterCategory.Utilities, costCenter.Category);
            Assert.True(costCenter.IsActive);
        }

        [Fact]
        public void AuditItemDetail_AllowsLineLevelBranchOrCostCenterAssignment()
        {
            var detail = new AuditItemDetail
            {
                ItemName = "Water",
                Quantity = 1,
                Price = 100m,
                Total = 100m,
                AssignedEstablishmentId = 2,
                CostCenterId = null,
                ReceiptStatus = ReceiptLineStatus.HasReceipt
            };

            Assert.Equal(2, detail.AssignedEstablishmentId);
            Assert.Null(detail.CostCenterId);
            Assert.Equal(ReceiptLineStatus.HasReceipt, detail.ReceiptStatus);
        }
    }

    public class SalesReportModelTests
    {
        [Fact]
        public void SalesReport_ConfirmedCashToHandover_IsEditableBeforeConfirmation()
        {
            var report = new SalesReport
            {
                EstablishmentId = 1,
                BusinessDate = new DateTime(2026, 8, 5),
                HandoverDate = new DateTime(2026, 8, 6),
                GrossSales = 29528m,
                CashOut = 6858.29m,
                ConfirmedCashToHandover = 22669.71m,
                Status = SalesReportStatus.Draft
            };

            Assert.Equal(SalesReportStatus.Draft, report.Status);
            Assert.Equal(22669.71m, report.ConfirmedCashToHandover);
        }

        [Fact]
        public void DocumentRecord_CanRepresentSalesReportDocument()
        {
            var document = new DocumentRecord
            {
                DocumentType = DocumentType.DailySalesReport,
                ImageUrl = "/SalesReports/Document/sample.jpg",
                OcrStatus = OcrStatus.Parsed,
                ReviewStatus = DocumentReviewStatus.Draft
            };

            Assert.Equal(DocumentType.DailySalesReport, document.DocumentType);
            Assert.Equal(OcrStatus.Parsed, document.OcrStatus);
            Assert.Equal(DocumentReviewStatus.Draft, document.ReviewStatus);
        }

        [Fact]
        public void SalesReport_CanPersistCashBreakdownLinesThroughCollection()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new AuditDbContext(options))
            {
                context.Database.EnsureCreated();

                var uploader = new User
                {
                    Name = "Uploader",
                    Email = "uploader@example.com",
                    PasswordHash = "hash",
                    Role = UserRole.BranchStaff
                };
                var establishment = new Establishment { Name = "CKR Branch 5" };
                context.Users.Add(uploader);
                context.Establishments.Add(establishment);
                context.SaveChanges();

                var document = new DocumentRecord
                {
                    DocumentType = DocumentType.DailySalesReport,
                    UploadedByUserId = uploader.Id,
                    ImageUrl = "/SalesReports/Document/sample.jpg",
                    OcrStatus = OcrStatus.Parsed,
                    ReviewStatus = DocumentReviewStatus.Draft
                };

                var report = new SalesReport
                {
                    DocumentRecord = document,
                    EstablishmentId = establishment.Id,
                    BusinessDate = new DateTime(2026, 8, 5),
                    HandoverDate = new DateTime(2026, 8, 6),
                    GrossSales = 29528m,
                    CashOut = 6858.29m,
                    ConfirmedCashToHandover = 22669.71m,
                    Status = SalesReportStatus.Draft
                };
                report.CashBreakdownLines.Add(new CashBreakdownLine
                {
                    OwnerType = CashBreakdownOwnerType.SalesReport,
                    Denomination = 1000m,
                    Quantity = 22,
                    Total = 22000m
                });

                context.SalesReports.Add(report);
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(options))
            {
                var savedReport = context.SalesReports
                    .Include(r => r.CashBreakdownLines)
                    .Single();

                var line = Assert.Single(savedReport.CashBreakdownLines);
                Assert.Equal(CashBreakdownOwnerType.SalesReport, line.OwnerType);
                Assert.Equal(savedReport.Id, line.SalesReportId);
                Assert.Equal(22000m, line.Total);
            }
        }

        [Fact]
        public void DocumentRecord_UserRelationships_RestrictDeletes()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var documentRecord = context.Model.FindEntityType(typeof(DocumentRecord))!;
            var uploadedBy = documentRecord.GetForeignKeys()
                .Single(fk => fk.Properties.Single().Name == nameof(DocumentRecord.UploadedByUserId));
            var confirmedBy = documentRecord.GetForeignKeys()
                .Single(fk => fk.Properties.Single().Name == nameof(DocumentRecord.ConfirmedByUserId));

            Assert.Equal(DeleteBehavior.Restrict, uploadedBy.DeleteBehavior);
            Assert.Equal(DeleteBehavior.Restrict, confirmedBy.DeleteBehavior);
        }
    }

    public class TreasuryReportViewModelTests
    {
        [Fact]
        public void TreasuryReportSummary_CanSeparateBranchAndCostCenterTotals()
        {
            var summary = new TreasuryReportSummary
            {
                Label = "CKR Main",
                BranchTotal = 29528m,
                CostCenterTotal = 0m
            };

            Assert.Equal("CKR Main", summary.Label);
            Assert.Equal(29528m, summary.BranchTotal);
            Assert.Equal(0m, summary.CostCenterTotal);
        }
    }

    public class TreasuryCashFlowTests
    {
        [Fact]
        public void CashFlow_RecomputesTotalsFromEntries()
        {
            var flow = new TreasuryCashFlow
            {
                TreasuryUserId = 1,
                CashFlowDate = new DateTime(2026, 8, 6),
                StartingBalance = 48212m,
                Entries = new List<CashFlowEntry>
                {
                    new CashFlowEntry { Direction = CashFlowDirection.In, Category = CashFlowCategory.Sales, Amount = 29528m },
                    new CashFlowEntry { Direction = CashFlowDirection.Out, Category = CashFlowCategory.PcfRelease, Amount = 8000m }
                }
            };

            flow.RecomputeTotals();

            Assert.Equal(29528m, flow.TotalCashIn);
            Assert.Equal(8000m, flow.TotalCashOut);
            Assert.Equal(77740m, flow.NetCashFlow);
            Assert.Equal(69740m, flow.ClosingBalance);
        }
    }

    public class TreasuryDashboardTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public TreasuryDashboardTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AuditDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private static TreasuryController CreateController(AuditDbContext context)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, "Owner")
            };

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            };

            return new TreasuryController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                }
            };
        }

        [Fact]
        public void Index_LoadsSelectedDayEntriesAndReturnsRecomputedTotals()
        {
            var selectedDate = new DateTime(2026, 8, 10, 14, 30, 0);

            using (var context = new AuditDbContext(_options))
            {
                var treasuryUser = new User { Name = "Treasury Owner", Email = "treasury-owner@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true };
                var relatedUser = new User { Name = "Branch Staff", Email = "branch-staff-dashboard@test.com", PasswordHash = "hash", Role = UserRole.BranchStaff };
                var establishment = new Establishment { Name = "CKR Main" };
                var costCenter = new CostCenter { Name = "Operations" };
                var sourceDocument = new DocumentRecord
                {
                    DocumentType = DocumentType.DailySalesReport,
                    UploadedByUser = relatedUser,
                    ImageUrl = "/sales-report.jpg",
                    OcrStatus = OcrStatus.Parsed,
                    ReviewStatus = DocumentReviewStatus.Confirmed
                };

                context.Users.AddRange(treasuryUser, relatedUser);
                context.Establishments.Add(establishment);
                context.CostCenters.Add(costCenter);
                context.DocumentRecords.Add(sourceDocument);
                context.SaveChanges();

                context.TreasuryCashFlows.AddRange(
                    new TreasuryCashFlow
                    {
                        TreasuryUserId = treasuryUser.Id,
                        CashFlowDate = selectedDate.Date,
                        StartingBalance = 1000m,
                        TotalCashIn = 1m,
                        TotalCashOut = 1m,
                        NetCashFlow = 1m,
                        ClosingBalance = 1m,
                        Entries = new List<CashFlowEntry>
                        {
                            new CashFlowEntry
                            {
                                Direction = CashFlowDirection.In,
                                Category = CashFlowCategory.Sales,
                                Amount = 2500m,
                                EstablishmentId = establishment.Id,
                                RelatedUserId = relatedUser.Id,
                                SourceDocumentId = sourceDocument.Id,
                                Notes = "Daily handover",
                                CreatedByUserId = treasuryUser.Id
                            },
                            new CashFlowEntry
                            {
                                Direction = CashFlowDirection.Out,
                                Category = CashFlowCategory.PcfRelease,
                                Amount = 400m,
                                CostCenterId = costCenter.Id,
                                RelatedUserId = relatedUser.Id,
                                Notes = "PCF release",
                                CreatedByUserId = treasuryUser.Id
                            }
                        }
                    },
                    new TreasuryCashFlow
                    {
                        TreasuryUserId = treasuryUser.Id,
                        CashFlowDate = selectedDate.Date.AddDays(1),
                        StartingBalance = 9999m
                    });
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = CreateController(context);
                var cashFlowCountBefore = context.TreasuryCashFlows.Count();

                var result = controller.Index(selectedDate);

                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<TreasuryCashFlowViewModel>(viewResult.Model);

                Assert.Equal(selectedDate.Date, model.CashFlowDate);
                Assert.NotNull(model.FlowId);
                Assert.Equal(TreasuryCashFlowStatus.Open, model.Status);
                Assert.Equal(1000m, model.StartingBalance);
                Assert.Equal(2500m, model.TotalCashIn);
                Assert.Equal(400m, model.TotalCashOut);
                Assert.Equal(3500m, model.NetCashFlow);
                Assert.Equal(3100m, model.ClosingBalance);

                Assert.Equal(2, model.Entries.Count);
                Assert.Contains(model.Entries, entry => entry.Establishment?.Name == "CKR Main" && entry.SourceDocument?.Id > 0);
                Assert.Contains(model.Entries, entry => entry.CostCenter?.Name == "Operations" && entry.RelatedUser?.Name == "Branch Staff");
                Assert.Equal(cashFlowCountBefore, context.TreasuryCashFlows.Count());
            }
        }

        [Fact]
        public void Index_ForNoFlowDateReturnsZeroTotalsAndNoEntriesWithoutCreatingFlow()
        {
            using (var context = new AuditDbContext(_options))
            {
                context.Users.Add(new User { Name = "Treasury Owner", Email = "empty-treasury-owner@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true });
                context.SaveChanges();
                context.TreasuryCashFlows.Add(new TreasuryCashFlow
                {
                    TreasuryUserId = context.Users.Single().Id,
                    CashFlowDate = new DateTime(2026, 8, 9),
                    StartingBalance = 500m
                });
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = CreateController(context);
                var cashFlowCountBefore = context.TreasuryCashFlows.Count();

                var result = controller.Index(new DateTime(2026, 8, 10));

                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<TreasuryCashFlowViewModel>(viewResult.Model);

                Assert.Equal(new DateTime(2026, 8, 10), model.CashFlowDate);
                Assert.Null(model.FlowId);
                Assert.Null(model.Status);
                Assert.Empty(model.Entries);
                Assert.Equal(0m, model.StartingBalance);
                Assert.Equal(0m, model.TotalCashIn);
                Assert.Equal(0m, model.TotalCashOut);
                Assert.Equal(0m, model.NetCashFlow);
                Assert.Equal(0m, model.ClosingBalance);
                Assert.Equal(cashFlowCountBefore, context.TreasuryCashFlows.Count());
            }
        }
    }

    public class PcfReleaseUsabilityTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public PcfReleaseUsabilityTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AuditDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private static TreasuryController CreateController(AuditDbContext context, int currentUserId = 1)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()),
                new Claim(ClaimTypes.Role, "Owner")
            };

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            };

            return new TreasuryController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
            };
        }

        private static async Task SeedReleaseLookupsAsync(AuditDbContext context)
        {
            context.Establishments.AddRange(
                new Establishment
                {
                    Id = 1,
                    Name = "Operating Branch",
                    IsOperatingBranch = true,
                    IsActive = true
                },
                new Establishment
                {
                    Id = 2,
                    Name = "Inactive Branch",
                    IsOperatingBranch = true,
                    IsActive = false
                },
                new Establishment
                {
                    Id = 3,
                    Name = "Miscellaneous Location",
                    IsOperatingBranch = true,
                    IsActive = true,
                    IsMiscellaneous = true
                });

            context.Users.AddRange(
                new User
                {
                    Id = 1,
                    Name = "Treasury Owner",
                    Email = "pcf-release-treasury@test.com",
                    PasswordHash = "hash",
                    Role = UserRole.Owner,
                    IsTreasury = true
                },
                new User
                {
                    Id = 2,
                    Name = "Branch Receiver",
                    Email = "pcf-release-receiver@test.com",
                    PasswordHash = "hash",
                    Role = UserRole.BranchStaff,
                    EstablishmentId = 1
                });

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task ReleasePcf_GetLoadsUsableFormLookupsAndDefaultsDateToToday()
        {
            using var context = new AuditDbContext(_options);
            await SeedReleaseLookupsAsync(context);
            var controller = CreateController(context);

            var result = await controller.ReleasePcf();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<PcfReleaseViewModel>(viewResult.Model);
            Assert.Equal(DateTime.Today, model.ReleaseDate.Date);

            var receivers = Assert.IsAssignableFrom<SelectList>((object)controller.ViewBag.ReceiverUsers);
            Assert.Contains(receivers, item => item.Value == "2" && item.Text == "Branch Receiver");

            var establishments = Assert.IsAssignableFrom<SelectList>((object)controller.ViewBag.Establishments);
            Assert.Contains(establishments, item => item.Value == "1" && item.Text == "Operating Branch");
            Assert.DoesNotContain(establishments, item => item.Value == "2");
            Assert.DoesNotContain(establishments, item => item.Value == "3");
        }

        [Fact]
        public async Task ReleasePcf_PostSavesReleaseCashOutEntryAndRecomputesDailyTotals()
        {
            using var context = new AuditDbContext(_options);
            await SeedReleaseLookupsAsync(context);
            var releaseDate = new DateTime(2026, 8, 11);
            var controller = CreateController(context, currentUserId: 1);
            var model = new PcfReleaseViewModel
            {
                ReleaseDate = releaseDate,
                Amount = 750m,
                ReceiverUserId = 2,
                ReceiverName = "External Receiver",
                EstablishmentId = 1,
                Purpose = "Branch replenishment"
            };

            var result = await controller.ReleasePcf(model);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(TreasuryController.Index), redirect.ActionName);
            Assert.Equal("Treasury", redirect.ControllerName);
            Assert.Equal(releaseDate.Date, redirect.RouteValues?["date"]);
            Assert.Equal("PCF release saved.", controller.TempData["Message"]);

            var release = await context.PcfReleases.AsNoTracking().SingleAsync();
            Assert.Equal(1, release.ReleasedByTreasuryUserId);
            Assert.Equal(2, release.ReceiverUserId);
            Assert.Equal("External Receiver", release.ReceiverName);
            Assert.Equal(1, release.EstablishmentId);
            Assert.Equal(750m, release.Amount);
            Assert.Equal(releaseDate.Date, release.ReleaseDate);
            Assert.Equal("Branch replenishment", release.Purpose);
            Assert.NotNull(release.CashFlowEntryId);

            var flow = await context.TreasuryCashFlows
                .Include(f => f.Entries)
                .AsNoTracking()
                .SingleAsync(f => f.CashFlowDate == releaseDate.Date);
            Assert.Equal(1, flow.TreasuryUserId);
            Assert.Equal(0m, flow.StartingBalance);
            Assert.Equal(0m, flow.TotalCashIn);
            Assert.Equal(750m, flow.TotalCashOut);
            Assert.Equal(0m, flow.NetCashFlow);
            Assert.Equal(-750m, flow.ClosingBalance);

            var entry = Assert.Single(flow.Entries);
            Assert.Equal(release.CashFlowEntryId, entry.Id);
            Assert.Equal(CashFlowDirection.Out, entry.Direction);
            Assert.Equal(CashFlowCategory.PcfRelease, entry.Category);
            Assert.Equal(750m, entry.Amount);
            Assert.Equal(1, entry.EstablishmentId);
            Assert.Equal(2, entry.RelatedUserId);
            Assert.Equal("Branch replenishment", entry.Notes);
            Assert.Equal(1, entry.CreatedByUserId);
        }

        [Fact]
        public async Task ReleasePcf_PostReusesExistingDailyFlowAndRecomputesTotals()
        {
            using var context = new AuditDbContext(_options);
            await SeedReleaseLookupsAsync(context);
            var releaseDate = new DateTime(2026, 8, 12);
            context.TreasuryCashFlows.Add(new TreasuryCashFlow
            {
                TreasuryUserId = 1,
                CashFlowDate = releaseDate,
                StartingBalance = 100m,
                Entries = new List<CashFlowEntry>
                {
                    new CashFlowEntry
                    {
                        Direction = CashFlowDirection.In,
                        Category = CashFlowCategory.OwnerFunding,
                        Amount = 300m,
                        CreatedByUserId = 1
                    }
                }
            });
            await context.SaveChangesAsync();
            var existingFlow = await context.TreasuryCashFlows.Include(f => f.Entries).SingleAsync();
            existingFlow.RecomputeTotals();
            await context.SaveChangesAsync();
            var flowId = existingFlow.Id;
            context.ChangeTracker.Clear();

            var controller = CreateController(context, currentUserId: 1);

            await controller.ReleasePcf(new PcfReleaseViewModel
            {
                ReleaseDate = releaseDate,
                Amount = 125m,
                ReceiverUserId = 2,
                EstablishmentId = 1
            });

            var flow = await context.TreasuryCashFlows
                .Include(f => f.Entries)
                .AsNoTracking()
                .SingleAsync(f => f.CashFlowDate == releaseDate);
            Assert.Equal(flowId, flow.Id);
            Assert.Equal(100m, flow.StartingBalance);
            Assert.Equal(300m, flow.TotalCashIn);
            Assert.Equal(125m, flow.TotalCashOut);
            Assert.Equal(400m, flow.NetCashFlow);
            Assert.Equal(275m, flow.ClosingBalance);
            Assert.Equal(2, flow.Entries.Count);
        }

        [Fact]
        public async Task ReleasePcf_PostInvalidAmountDoesNotSaveAndRepopulatesLookups()
        {
            using var context = new AuditDbContext(_options);
            await SeedReleaseLookupsAsync(context);
            var controller = CreateController(context);
            var model = new PcfReleaseViewModel
            {
                ReleaseDate = new DateTime(2026, 8, 13),
                Amount = 0m,
                ReceiverUserId = 2,
                EstablishmentId = 1
            };

            var result = await controller.ReleasePcf(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey(nameof(PcfReleaseViewModel.Amount)));
            Assert.Empty(context.PcfReleases);
            Assert.Empty(context.TreasuryCashFlows);
            Assert.Empty(context.CashFlowEntries);
            Assert.IsAssignableFrom<SelectList>(controller.ViewBag.ReceiverUsers);
            Assert.IsAssignableFrom<SelectList>(controller.ViewBag.Establishments);
        }

        [Fact]
        public async Task ReleasePcf_PostInvalidBranchDoesNotSaveAndRepopulatesLookups()
        {
            using var context = new AuditDbContext(_options);
            await SeedReleaseLookupsAsync(context);
            var controller = CreateController(context);
            var model = new PcfReleaseViewModel
            {
                ReleaseDate = new DateTime(2026, 8, 14),
                Amount = 50m,
                ReceiverUserId = 2,
                EstablishmentId = 3
            };

            var result = await controller.ReleasePcf(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey(nameof(PcfReleaseViewModel.EstablishmentId)));
            Assert.Empty(context.PcfReleases);
            Assert.Empty(context.TreasuryCashFlows);
            Assert.Empty(context.CashFlowEntries);
            var establishments = Assert.IsAssignableFrom<SelectList>((object)controller.ViewBag.Establishments);
            Assert.DoesNotContain(establishments, item => item.Value == "3");
        }

        [Fact]
        public async Task ReleasePcf_PostOverlongReceiverNameAndPurposeDoesNotSave()
        {
            using var context = new AuditDbContext(_options);
            await SeedReleaseLookupsAsync(context);
            var controller = CreateController(context);
            var model = new PcfReleaseViewModel
            {
                ReleaseDate = new DateTime(2026, 8, 15),
                Amount = 50m,
                ReceiverUserId = 2,
                EstablishmentId = 1,
                ReceiverName = new string('R', 101),
                Purpose = new string('P', 256)
            };

            var result = await controller.ReleasePcf(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey(nameof(PcfReleaseViewModel.ReceiverName)));
            Assert.True(controller.ModelState.ContainsKey(nameof(PcfReleaseViewModel.Purpose)));
            Assert.Empty(context.PcfReleases);
            Assert.Empty(context.TreasuryCashFlows);
            Assert.Empty(context.CashFlowEntries);
        }
    }

    public class SalesReportUsabilityTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public SalesReportUsabilityTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AuditDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private static SalesReportsController CreateController(AuditDbContext context, int currentUserId = 1, string currentUserRole = "Owner")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()),
                new Claim(ClaimTypes.Role, currentUserRole)
            };

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            };

            return new SalesReportsController(context, new FakeOcrService())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
            };
        }

        private static async Task<SalesReport> SeedDraftSalesReportAsync(AuditDbContext context)
        {
            var treasuryUser = new User
            {
                Id = 1,
                Name = "Treasury Owner",
                Email = "sales-report-owner@test.com",
                PasswordHash = "hash",
                Role = UserRole.Owner,
                IsTreasury = true
            };

            var branchStaff = new User
            {
                Id = 2,
                Name = "Branch Staff",
                Email = "sales-report-branch@test.com",
                PasswordHash = "hash",
                Role = UserRole.BranchStaff,
                EstablishmentId = 1
            };

            var outsideBranchStaff = new User
            {
                Id = 3,
                Name = "Outside Branch Staff",
                Email = "sales-report-outside-branch@test.com",
                PasswordHash = "hash",
                Role = UserRole.BranchStaff,
                EstablishmentId = 2
            };

            var establishment = new Establishment
            {
                Id = 1,
                Name = "CKR Sales Branch",
                IsOperatingBranch = true,
                IsActive = true
            };

            var otherEstablishment = new Establishment
            {
                Id = 2,
                Name = "CKR Other Branch",
                IsOperatingBranch = true,
                IsActive = true
            };

            context.Users.AddRange(treasuryUser, branchStaff, outsideBranchStaff);
            context.Establishments.AddRange(establishment, otherEstablishment);
            await context.SaveChangesAsync();

            var document = new DocumentRecord
            {
                DocumentType = DocumentType.DailySalesReport,
                UploadedByUserId = treasuryUser.Id,
                ImageUrl = "/SalesReports/Image/sales-report.jpg",
                OcrStatus = OcrStatus.Parsed,
                ReviewStatus = DocumentReviewStatus.Draft
            };

            context.DocumentRecords.Add(document);
            await context.SaveChangesAsync();

            var report = new SalesReport
            {
                DocumentRecordId = document.Id,
                EstablishmentId = establishment.Id,
                CashierName = "Initial Cashier",
                BusinessDate = new DateTime(2026, 8, 9),
                HandoverDate = new DateTime(2026, 8, 10),
                GrossSales = 1000m,
                CashOut = 100m,
                ConfirmedCashToHandover = 900m,
                GCashAmount = 50m,
                CreditAmount = 25m,
                OtherPaymentAmount = 10m,
                ReceiptNumberStart = "A-100",
                ReceiptNumberEnd = "A-199",
                WitnessName = "Initial Witness",
                Notes = "Initial notes",
                Status = SalesReportStatus.Draft
            };

            context.SalesReports.Add(report);
            await context.SaveChangesAsync();

            return report;
        }

        private static SalesReportReviewViewModel BuildReviewModel(SalesReport report, decimal confirmedCash)
        {
            return new SalesReportReviewViewModel
            {
                SalesReportId = report.Id,
                DocumentRecordId = report.DocumentRecordId,
                EstablishmentId = report.EstablishmentId,
                CashierName = "Updated Cashier",
                BusinessDate = new DateTime(2026, 8, 9),
                HandoverDate = new DateTime(2026, 8, 10),
                GrossSales = confirmedCash + 100m,
                CashOut = 100m,
                ConfirmedCashToHandover = confirmedCash,
                GCashAmount = 75m,
                CreditAmount = 50m,
                OtherPaymentAmount = 25m,
                ReceiptNumberStart = "B-200",
                ReceiptNumberEnd = "B-299",
                WitnessName = "Updated Witness",
                Notes = "Updated notes",
                ImageUrl = "/SalesReports/Image/sales-report.jpg"
            };
        }

        [Fact]
        public async Task Review_ConfirmCreatesThenUpdatesOneSalesCashInEntryAndRecomputesFlow()
        {
            using var context = new AuditDbContext(_options);
            var report = await SeedDraftSalesReportAsync(context);

            var firstController = CreateController(context);
            var firstModel = BuildReviewModel(report, 1250m);

            var firstResult = await firstController.Review(firstModel, "Confirm");

            var firstRedirect = Assert.IsType<RedirectToActionResult>(firstResult);
            Assert.Equal(nameof(SalesReportsController.Review), firstRedirect.ActionName);

            var firstEntry = Assert.Single(await context.CashFlowEntries.AsNoTracking().ToListAsync());
            Assert.Equal(CashFlowDirection.In, firstEntry.Direction);
            Assert.Equal(CashFlowCategory.Sales, firstEntry.Category);
            Assert.Equal(1250m, firstEntry.Amount);
            Assert.Equal(report.EstablishmentId, firstEntry.EstablishmentId);
            Assert.Equal(report.DocumentRecordId, firstEntry.SourceDocumentId);

            var firstFlow = await context.TreasuryCashFlows
                .Include(f => f.Entries)
                .AsNoTracking()
                .SingleAsync(f => f.CashFlowDate == firstModel.HandoverDate.Date);
            Assert.Equal(1250m, firstFlow.TotalCashIn);
            Assert.Equal(0m, firstFlow.TotalCashOut);
            Assert.Equal(1250m, firstFlow.NetCashFlow);
            Assert.Equal(1250m, firstFlow.ClosingBalance);
            Assert.Single(firstFlow.Entries);

            context.ChangeTracker.Clear();

            var secondController = CreateController(context);
            var secondModel = BuildReviewModel(report, 1750m);

            var secondResult = await secondController.Review(secondModel, "Confirm");

            var secondRedirect = Assert.IsType<RedirectToActionResult>(secondResult);
            Assert.Equal(nameof(SalesReportsController.Review), secondRedirect.ActionName);

            var updatedEntry = Assert.Single(await context.CashFlowEntries.AsNoTracking().ToListAsync());
            Assert.Equal(firstEntry.Id, updatedEntry.Id);
            Assert.Equal(CashFlowDirection.In, updatedEntry.Direction);
            Assert.Equal(CashFlowCategory.Sales, updatedEntry.Category);
            Assert.Equal(1750m, updatedEntry.Amount);
            Assert.Equal(report.EstablishmentId, updatedEntry.EstablishmentId);
            Assert.Equal(report.DocumentRecordId, updatedEntry.SourceDocumentId);
            Assert.Equal(1, updatedEntry.ConfirmedByUserId);

            var updatedFlow = await context.TreasuryCashFlows
                .Include(f => f.Entries)
                .AsNoTracking()
                .SingleAsync(f => f.CashFlowDate == secondModel.HandoverDate.Date);
            Assert.Equal(1750m, updatedFlow.TotalCashIn);
            Assert.Equal(0m, updatedFlow.TotalCashOut);
            Assert.Equal(1750m, updatedFlow.NetCashFlow);
            Assert.Equal(1750m, updatedFlow.ClosingBalance);
            Assert.Single(updatedFlow.Entries);

            var savedReport = await context.SalesReports.AsNoTracking().SingleAsync();
            Assert.Equal(SalesReportStatus.Confirmed, savedReport.Status);
            Assert.Equal(1, savedReport.ConfirmedByUserId);
            Assert.NotNull(savedReport.ConfirmedAt);
            Assert.Equal(1750m, savedReport.ConfirmedCashToHandover);

            var savedDocument = await context.DocumentRecords.AsNoTracking().SingleAsync();
            Assert.Equal(DocumentReviewStatus.Confirmed, savedDocument.ReviewStatus);
            Assert.Equal(1, savedDocument.ConfirmedByUserId);
            Assert.NotNull(savedDocument.ConfirmedAt);
        }

        [Fact]
        public async Task Review_SaveDraftUpdatesReportWithoutPostingTreasuryCashFlow()
        {
            using var context = new AuditDbContext(_options);
            var report = await SeedDraftSalesReportAsync(context);
            var controller = CreateController(context);
            var model = BuildReviewModel(report, 1500m);

            var result = await controller.Review(model, "SaveDraft");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SalesReportsController.Review), redirect.ActionName);
            Assert.Empty(await context.TreasuryCashFlows.AsNoTracking().ToListAsync());
            Assert.Empty(await context.CashFlowEntries.AsNoTracking().ToListAsync());

            var savedReport = await context.SalesReports.AsNoTracking().SingleAsync();
            Assert.Equal(SalesReportStatus.Draft, savedReport.Status);
            Assert.Null(savedReport.ConfirmedByUserId);
            Assert.Null(savedReport.ConfirmedAt);
            Assert.Equal("Updated Cashier", savedReport.CashierName);
            Assert.Equal(1500m, savedReport.ConfirmedCashToHandover);

            var savedDocument = await context.DocumentRecords.AsNoTracking().SingleAsync();
            Assert.Equal(DocumentReviewStatus.Draft, savedDocument.ReviewStatus);
            Assert.Null(savedDocument.ConfirmedByUserId);
            Assert.Null(savedDocument.ConfirmedAt);
        }

        [Fact]
        public async Task Upload_ForBranchStaffShowsOnlyAssignedOperatingBranch()
        {
            using var context = new AuditDbContext(_options);
            await SeedDraftSalesReportAsync(context);
            var controller = CreateController(context, 2, "BranchStaff");

            var result = await controller.Upload();

            Assert.IsType<ViewResult>(result);
            var establishments = Assert.IsType<SelectList>((object)controller.ViewBag.Establishments);
            var item = Assert.Single(establishments.AsEnumerable().ToList());
            Assert.Equal("1", item.Value);
            Assert.Equal("CKR Sales Branch", item.Text);
        }

        [Fact]
        public async Task Upload_PostForBranchStaffOutsideAssignedBranchIsForbiddenBeforeSaving()
        {
            using var context = new AuditDbContext(_options);
            await SeedDraftSalesReportAsync(context);
            var controller = CreateController(context, 2, "BranchStaff");

            var result = await controller.Upload(2, new DateTime(2026, 8, 9), new DateTime(2026, 8, 10), "Cashier", null);

            Assert.IsType<ForbidResult>(result);
            Assert.Equal(1, await context.DocumentRecords.CountAsync());
            Assert.Equal(1, await context.SalesReports.CountAsync());
        }

        [Fact]
        public async Task Review_ForBranchStaffOutsideAssignedBranchIsForbidden()
        {
            using var context = new AuditDbContext(_options);
            var report = await SeedDraftSalesReportAsync(context);
            var controller = CreateController(context, 3, "BranchStaff");
            var model = BuildReviewModel(report, 1500m);

            var getResult = await controller.Review(report.Id);
            var postResult = await controller.Review(model, "Confirm");

            Assert.IsType<ForbidResult>(getResult);
            Assert.IsType<ForbidResult>(postResult);
            Assert.Empty(await context.CashFlowEntries.AsNoTracking().ToListAsync());

            var savedReport = await context.SalesReports.AsNoTracking().SingleAsync();
            Assert.Equal(SalesReportStatus.Draft, savedReport.Status);
            Assert.Equal(900m, savedReport.ConfirmedCashToHandover);
        }

        [Fact]
        public async Task Review_SaveDraftAfterConfirmationIsBlockedWithoutChangingConfirmedStateOrEntry()
        {
            using var context = new AuditDbContext(_options);
            var report = await SeedDraftSalesReportAsync(context);
            var confirmController = CreateController(context);
            var confirmModel = BuildReviewModel(report, 1250m);
            await confirmController.Review(confirmModel, "Confirm");

            context.ChangeTracker.Clear();

            var draftController = CreateController(context);
            var draftModel = BuildReviewModel(report, 999m);

            var result = await draftController.Review(draftModel, "SaveDraft");

            Assert.IsType<ViewResult>(result);
            Assert.False(draftController.ModelState.IsValid);
            Assert.Equal("Confirmed sales reports cannot be saved as drafts.", draftController.TempData["Error"]);

            var savedReport = await context.SalesReports.AsNoTracking().SingleAsync();
            Assert.Equal(SalesReportStatus.Confirmed, savedReport.Status);
            Assert.Equal(1, savedReport.ConfirmedByUserId);
            Assert.NotNull(savedReport.ConfirmedAt);
            Assert.Equal(1250m, savedReport.ConfirmedCashToHandover);

            var savedDocument = await context.DocumentRecords.AsNoTracking().SingleAsync();
            Assert.Equal(DocumentReviewStatus.Confirmed, savedDocument.ReviewStatus);
            Assert.Equal(1, savedDocument.ConfirmedByUserId);
            Assert.NotNull(savedDocument.ConfirmedAt);

            var entry = Assert.Single(await context.CashFlowEntries.AsNoTracking().ToListAsync());
            Assert.Equal(1250m, entry.Amount);
        }

        [Fact]
        public async Task CashFlowEntries_RejectDuplicateSourceDocumentSalesCategory()
        {
            using var context = new AuditDbContext(_options);
            var report = await SeedDraftSalesReportAsync(context);

            context.TreasuryCashFlows.Add(new TreasuryCashFlow
            {
                TreasuryUserId = 1,
                CashFlowDate = report.HandoverDate.Date,
                Entries = new List<CashFlowEntry>
                {
                    new CashFlowEntry
                    {
                        Direction = CashFlowDirection.In,
                        Category = CashFlowCategory.Sales,
                        EstablishmentId = report.EstablishmentId,
                        SourceDocumentId = report.DocumentRecordId,
                        Amount = 100m,
                        CreatedByUserId = 1
                    },
                    new CashFlowEntry
                    {
                        Direction = CashFlowDirection.In,
                        Category = CashFlowCategory.Sales,
                        EstablishmentId = report.EstablishmentId,
                        SourceDocumentId = report.DocumentRecordId,
                        Amount = 200m,
                        CreatedByUserId = 1
                    }
                }
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    public class AuditSettlementTests
    {
        [Theory]
        [InlineData(5000, 3000, 2000, 0)]
        [InlineData(5000, 3000, 1500, -500)]
        [InlineData(5000, 3000, 2500, 500)]
        public void Settlement_ComputesShortOver(decimal released, decimal expenses, decimal actualChange, decimal expectedShortOver)
        {
            var settlement = new AuditSettlement
            {
                TotalPCReleased = released,
                TotalAcceptedExpenses = expenses,
                ActualChangeReturned = actualChange
            };

            settlement.Recompute();

            Assert.Equal(released - expenses, settlement.ExpectedChange);
            Assert.Equal(expectedShortOver, settlement.ShortOverAmount);
        }
    }
    public class CoverageUsabilityTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public CoverageUsabilityTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = new AuditDbContext(_options);
            context.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private static CoverageCreateForm NewCoverageForm(int coveredManagerId, int coveringManagerId, DateTime? startDate = null, DateTime? endDate = null)
        {
            return new CoverageCreateForm
            {
                CoveredManagerId = coveredManagerId,
                CoveringManagerId = coveringManagerId,
                StartDate = startDate ?? new DateTime(2026, 8, 11),
                EndDate = endDate ?? new DateTime(2026, 8, 12),
                Scope = CoverageScope.All,
                Reason = "Manager leave",
                IsActive = true
            };
        }

        private async Task SeedManagersAsync(AuditDbContext context)
        {
            context.Users.AddRange(
                new User { Id = 1, Name = "Alice Owner", Email = "coverage-owner@test.com", PasswordHash = "hash", Role = UserRole.Owner },
                new User { Id = 2, Name = "Bob Manager", Email = "coverage-bob@test.com", PasswordHash = "hash", Role = UserRole.Manager },
                new User { Id = 3, Name = "Cara Manager", Email = "coverage-cara@test.com", PasswordHash = "hash", Role = UserRole.Manager },
                new User { Id = 4, Name = "Drew Buyer", Email = "coverage-buyer@test.com", PasswordHash = "hash", Role = UserRole.Buyer },
                new User { Id = 5, Name = "Deleted Manager", Email = "coverage-deleted@test.com", PasswordHash = "hash", Role = UserRole.Manager, IsDeleted = true });

            await context.SaveChangesAsync();
        }

        private CoverageController CreateController(AuditDbContext context, int currentUserId = 1)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()),
                new Claim(ClaimTypes.Role, "Owner")
            };

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            };

            return new CoverageController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
            };
        }

        [Fact]
        public async Task Create_RejectsSameCoveredAndCoveringManager()
        {
            using var context = new AuditDbContext(_options);
            await SeedManagersAsync(context);
            var controller = CreateController(context);

            var result = await controller.Create(NewCoverageForm(2, 2));

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(controller.ModelState[nameof(CoverageCreateForm.CoveringManagerId)]!.Errors, error => error.ErrorMessage.Contains("different", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(await context.ManagerCoverages.ToListAsync());
            Assert.Equal("Covered and covering manager must be different.", controller.TempData["Error"]);
            Assert.NotNull(viewResult.Model);
        }

        [Fact]
        public async Task Create_RejectsInvalidDateRangeWithoutSaving()
        {
            using var context = new AuditDbContext(_options);
            await SeedManagersAsync(context);
            var controller = CreateController(context);

            var result = await controller.Create(NewCoverageForm(2, 3, new DateTime(2026, 8, 12), new DateTime(2026, 8, 11)));

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(controller.ModelState[nameof(CoverageCreateForm.EndDate)]!.Errors, error => error.ErrorMessage.Contains("End date", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(await context.ManagerCoverages.ToListAsync());
            Assert.Equal("End date must be on or after start date.", controller.TempData["Error"]);
        }

        [Fact]
        public async Task Create_RejectsNonManagerOrDeletedManagerWithoutSaving()
        {
            using var context = new AuditDbContext(_options);
            await SeedManagersAsync(context);
            var controller = CreateController(context);

            var result = await controller.Create(NewCoverageForm(2, 4));

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(controller.ModelState[nameof(CoverageCreateForm.CoveringManagerId)]!.Errors, error => error.ErrorMessage.Contains("active manager", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(await context.ManagerCoverages.ToListAsync());
            Assert.Equal("Covered and covering managers must be active managers.", controller.TempData["Error"]);
        }

        [Fact]
        public async Task Create_RejectsDeletedManagerWithoutSaving()
        {
            using var context = new AuditDbContext(_options);
            await SeedManagersAsync(context);
            var controller = CreateController(context);

            var result = await controller.Create(NewCoverageForm(5, 3));

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(controller.ModelState[nameof(CoverageCreateForm.CoveredManagerId)]!.Errors, error => error.ErrorMessage.Contains("active manager", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(await context.ManagerCoverages.ToListAsync());
            Assert.Equal("Covered and covering managers must be active managers.", controller.TempData["Error"]);
        }

        [Fact]
        public async Task Create_SavesValidCoverageWithCreatorAndActiveStatus()
        {
            using var context = new AuditDbContext(_options);
            await SeedManagersAsync(context);
            var controller = CreateController(context);

            var result = await controller.Create(NewCoverageForm(2, 3));

            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(CoverageController.Index), redirectResult.ActionName);

            var coverage = Assert.Single(await context.ManagerCoverages.ToListAsync());
            Assert.Equal(2, coverage.CoveredManagerId);
            Assert.Equal(3, coverage.CoveringManagerId);
            Assert.Equal(1, coverage.CreatedByUserId);
            Assert.True(coverage.IsActive);
            Assert.True(coverage.CoversDate(new DateTime(2026, 8, 11)));
            Assert.Equal("Coverage assignment created.", controller.TempData["Message"]);
        }
    }

    public class ManagerCoverageTests
    {
        [Fact]
        public void Coverage_IsActiveForDate_WhenDateIsWithinRange()
        {
            var coverage = new ManagerCoverage
            {
                CoveredManagerId = 1,
                CoveringManagerId = 2,
                StartDate = new DateTime(2026, 8, 10),
                EndDate = new DateTime(2026, 8, 12),
                IsActive = true
            };

            Assert.True(coverage.CoversDate(new DateTime(2026, 8, 11)));
            Assert.False(coverage.CoversDate(new DateTime(2026, 8, 13)));
        }
    }
}
}
