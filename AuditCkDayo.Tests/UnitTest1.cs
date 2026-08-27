using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
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

        [Fact]
        public async Task Delete_UserWithUploadedDocuments_ArchivesUserInsteadOfHardDeleting()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                
                // Add a document record uploaded by David Buyer (Id 4)
                context.DocumentRecords.Add(new DocumentRecord
                {
                    DocumentType = DocumentType.ExpenseReceipt,
                    UploadedByUserId = 4,
                    ImageUrl = "sample.jpg",
                    OcrStatus = OcrStatus.NotStarted,
                    ReviewStatus = DocumentReviewStatus.Uploaded
                });
                await context.SaveChangesAsync();

                var controller = CreateController(context, 1, "Admin");
                
                // Admin deletes David Buyer (Id 4)
                var result = await controller.Delete(4);

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Register", redirectResult.ActionName);
                Assert.Equal("User 'David Buyer' has been archived.", controller.TempData["Message"]);

                var buyer = await context.Users.FindAsync(4);
                Assert.NotNull(buyer);
                Assert.True(buyer.IsDeleted);
            }
        }


    public class FallbackOcrServiceTests
    {
        [Fact]
        public async Task ParseReceiptAsync_WhenGeminiFails_ReturnsEmptyOcrResult()
        {
            var configMap = new Dictionary<string, string>
            {
                { "GoogleGemini:ApiKey", "" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configMap!).Build();
            
            var gemini = new GoogleGeminiOcrService(config);
            var fallback = new FallbackOcrService(gemini);
            
            var dummyStream = new MemoryStream(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="));
            
            var result = await fallback.ParseReceiptAsync(new List<Stream> { dummyStream });
            
            Assert.NotNull(result);
            Assert.Null(result.TransactionDate);
            Assert.Equal(0m, result.TotalAmount);
        }

        [Fact]
        public void ReceiptTextParser_HandlesCurrencySymbolsAndCommaSeparatedAmounts()
        {
            var method = typeof(TesseractOcrService).GetMethod("ApplyReceiptText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);

            var result = new OcrResult();
            method!.Invoke(null, new object[]
            {
                result,
                "DATE: 08/11/2026\nRice 2 125.50 251.00\nTOTAL ₱1,234.50"
            });

            Assert.Equal(new DateTime(2026, 8, 11), result.TransactionDate);
            Assert.Equal(1234.50m, result.TotalAmount);
            Assert.Contains(result.Items, item => item.Name == "Rice" && item.Quantity == 2 && item.Total == 251.00m);
        }

        [Fact]
        public void SalesReportTextParser_HandlesClosingMainSalesGroupedAmounts()
        {
            var method = typeof(TesseractOcrService).GetMethod("ApplySalesReportText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);

            var result = new SalesReportOcrResult();
            method!.Invoke(null, new object[]
            {
                result,
                "August 12, 2026\n" +
                "WEDNESDAY\n" +
                "CLOSING\n" +
                "MAIN SALES\n" +
                "Cashier Name: Danica\n" +
                "Daily Gross Sales: ₱ 33,029.9\n" +
                "(Opening + Closing)\n" +
                "Closing Gross Sales: ₱ 17,475.40\n" +
                "Food Sales: ₱ 24,359.9\n" +
                "Beer Sales: ₱ 8,250\n" +
                "Beverages Sales: ₱ 120\n" +
                "Other Sales: ₱ 300\n" +
                "•Cash Sales: ₱ 6,023\n" +
                "•G-Cash sales: ₱\n" +
                "₱1,607\n" +
                "₱ 759\n" +
                "₱ 183.35\n" +
                "₱ 1,538.05\n" +
                "₱ 1,614\n" +
                "₱ 325\n" +
                "₱ 770\n" +
                "•Bank Transfer: ₱\n" +
                "₱ 2,224\n" +
                "₱ 651\n" +
                "₱ 1,106\n" +
                "•Card: ₱\n" +
                "•Credit: ₱\n" +
                "₱ - 253 Ma'am lolit\n" +
                "₱ - 241.20 Bryan/Paul\n" +
                "₱ - 100 paul\n" +
                "•Run-away Customer: ₱\n" +
                "Expenses from Sales: 830\n" +
                "- pita bread - 650\n" +
                "- willows cafe (ma'am barbs) - 180"
            });

            Assert.Equal(new DateTime(2026, 8, 12), result.BusinessDate);
            Assert.Equal("Danica", result.CashierName);
            Assert.Equal(33029.90m, result.GrossSales);
            Assert.Equal(830.00m, result.CashOut);
            Assert.Equal(6023.00m, result.ConfirmedCashToHandover);
            Assert.Equal(6796.40m, result.GCashAmount);
            Assert.Equal(594.20m, result.CreditAmount);
            Assert.Equal(3981.00m, result.OtherPaymentAmount);
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
        public void BatchReview_ShowsOnlyCurrentUsersPendingAuditDrafts()
        {
            using (var context = new AuditDbContext(_options))
            {
                var session = new FakeSession();
                var managerDrafts = new List<AuditSubmissionViewModel>
                {
                    new AuditSubmissionViewModel
                    {
                        Description = "Manager receipt",
                        Amount = 29.69m,
                        EntryDate = new DateTime(2026, 8, 13),
                        ReceiptImageUrl = "/Audits/Receipt/manager.png",
                        ReceiptImageUrls = new List<string> { "/Audits/Receipt/manager.png" }
                    }
                };
                var buyerDrafts = new List<AuditSubmissionViewModel>
                {
                    new AuditSubmissionViewModel
                    {
                        Description = "Buyer receipt",
                        Amount = 12.34m,
                        EntryDate = new DateTime(2026, 8, 13),
                        ReceiptImageUrl = "/Audits/Receipt/buyer.png",
                        ReceiptImageUrls = new List<string> { "/Audits/Receipt/buyer.png" }
                    }
                };
                session.SetString("PendingAuditDrafts", JsonSerializer.Serialize(managerDrafts));
                session.SetString("PendingAuditDrafts:3", JsonSerializer.Serialize(buyerDrafts));

                var controller = CreateController(context, 3, "Buyer", session);

                var result = controller.BatchReview();

                var viewResult = Assert.IsType<ViewResult>(result);
                var drafts = Assert.IsAssignableFrom<List<AuditSubmissionViewModel>>(viewResult.Model);
                var draft = Assert.Single(drafts);
                Assert.Equal("Buyer receipt", draft.Description);
            }
        }

        [Fact]
        public void BatchReview_ViewPreservesPnlCategoryIdWhenSavingDraftItems()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "Audits", "BatchReview.cshtml"));
            var view = File.ReadAllText(viewPath);

            Assert.Contains("pnlCategoryId", view);
            Assert.Contains("PnlCategoryId: item.pnlCategoryId", view);
            Assert.DoesNotContain("PnlSection: item.pnlSection", view);
            Assert.DoesNotContain("PnlCategoryName: item.pnlCategoryName", view);
        }

        [Fact]
        public void Upload_ShowsOnlyCurrentUsersPendingAuditDrafts()
        {
            using (var context = new AuditDbContext(_options))
            {
                var session = new FakeSession();
                var managerDrafts = new List<AuditSubmissionViewModel>
                {
                    new AuditSubmissionViewModel
                    {
                        Description = "Manager queued receipt",
                        Amount = 29.69m,
                        EntryDate = new DateTime(2026, 8, 13),
                        ReceiptImageUrl = "/Audits/Receipt/manager-upload.png",
                        ReceiptImageUrls = new List<string> { "/Audits/Receipt/manager-upload.png" }
                    }
                };
                var buyerDrafts = new List<AuditSubmissionViewModel>
                {
                    new AuditSubmissionViewModel
                    {
                        Description = "Buyer queued receipt",
                        Amount = 45.67m,
                        EntryDate = new DateTime(2026, 8, 13),
                        ReceiptImageUrl = "/Audits/Receipt/buyer-upload.png",
                        ReceiptImageUrls = new List<string> { "/Audits/Receipt/buyer-upload.png" }
                    }
                };
                session.SetString("PendingAuditDrafts", JsonSerializer.Serialize(managerDrafts));
                session.SetString("PendingAuditDrafts:3", JsonSerializer.Serialize(buyerDrafts));

                var controller = CreateController(context, 3, "Buyer", session);

                var result = controller.Upload();

                var viewResult = Assert.IsType<ViewResult>(result);
                var drafts = Assert.IsAssignableFrom<List<AuditSubmissionViewModel>>(viewResult.Model);
                var draft = Assert.Single(drafts);
                Assert.Equal("Buyer queued receipt", draft.Description);
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
        public async Task Receipt_AuthorizedPendingDraftBuyer_ReturnsFile()
        {
            var filename = "test_receipt_pending_draft.png";
            var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads");
            Directory.CreateDirectory(uploadsFolder);
            var filePath = Path.Combine(uploadsFolder, filename);
            var expectedBytes = new byte[] { 21, 22, 23 };
            await File.WriteAllBytesAsync(filePath, expectedBytes);

            try
            {
                using (var context = new AuditDbContext(_options))
                {
                    await SeedDataAsync(context);

                    var session = new FakeSession();
                    var drafts = new List<AuditSubmissionViewModel>
                    {
                        new AuditSubmissionViewModel
                        {
                            Description = "Pending draft receipt",
                            Amount = 29.69m,
                            EntryDate = new DateTime(2026, 8, 13),
                            ReceiptImageUrl = $"/Audits/Receipt/{filename}",
                            ReceiptImageUrls = new List<string> { $"/Audits/Receipt/{filename}" }
                        }
                    };
                    session.SetString("PendingAuditDrafts:3", JsonSerializer.Serialize(drafts));

                    var controller = CreateController(context, 3, "Buyer", session);
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
        public async Task SubmitAudit_BuyerLineItemsUseDestinationsWithoutPnlCategories()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                context.PnlCategories.AddRange(
                    new PnlCategory { Id = 20, Name = "Beers", Section = PnlExpenseSection.COGS },
                    new PnlCategory { Id = 21, Name = "LPG", Section = PnlExpenseSection.OPEX });
                await context.SaveChangesAsync();
                var auditController = CreateController(context, 3, "Buyer");
                var model = new AuditSubmissionViewModel
                {
                    EstablishmentId = 1,
                    CombinedDestinationId = "branch-1",
                    Amount = 180m,
                    Description = "Categorized receipt",
                    EntryDate = DateTime.Today,
                    ReceiptImageUrl = "/Audits/Receipt/pnl-receipt.png",
                    ReceiptImageUrls = new List<string> { "/Audits/Receipt/pnl-receipt.png" },
                    Items = new List<OcrItemResult>
                    {
                        new OcrItemResult { Name = "San Mig", Quantity = 1, Price = 80m, Total = 80m, CombinedDestinationId = "branch-1", PnlCategoryId = 20 },
                        new OcrItemResult { Name = "Gas", Quantity = 1, Price = 100m, Total = 100m, CombinedDestinationId = "branch-2", PnlCategoryId = 21 }
                    }
                };

                var submitResult = await auditController.SubmitAudit(model);

                Assert.IsType<RedirectToActionResult>(submitResult);
                var savedAudit = await context.AuditItems
                    .Include(a => a.Details)
                    .SingleAsync(a => a.Description == "Categorized receipt");
                Assert.All(savedAudit.Details, detail => Assert.Null(detail.PnlCategoryId));
                Assert.Contains(savedAudit.Details, detail => detail.ItemName == "San Mig" && detail.AssignedEstablishmentId == 1);
                Assert.Contains(savedAudit.Details, detail => detail.ItemName == "Gas" && detail.AssignedEstablishmentId == 2);
            }
        }

        [Fact]
        public async Task SubmitAudit_BranchStaffPersistsPnlCategoriesOnReceiptLines()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                context.Users.Add(new User { Id = 8, Name = "Branch Staff", Email = "branch@test.com", PasswordHash = "hash", Role = UserRole.BranchStaff, PcfBalance = 500m, DailyStartingFloat = 500m, ManagerId = 2, EstablishmentId = 1 });
                var branchStaffEst = context.Establishments.Single(e => e.Id == 1);
                branchStaffEst.PcfBalance = 500m;
                branchStaffEst.DailyStartingFloat = 500m;
                context.PnlCategories.AddRange(
                    new PnlCategory { Id = 20, Name = "Beers", Section = PnlExpenseSection.COGS },
                    new PnlCategory { Id = 21, Name = "LPG", Section = PnlExpenseSection.OPEX });
                await context.SaveChangesAsync();
                var auditController = CreateController(context, 8, "BranchStaff");
                var model = new AuditSubmissionViewModel
                {
                    EstablishmentId = 1,
                    CombinedDestinationId = "branch-1",
                    Amount = 180m,
                    Description = "Branch categorized receipt",
                    EntryDate = DateTime.Today,
                    ReceiptImageUrl = "/Audits/Receipt/pnl-receipt.png",
                    ReceiptImageUrls = new List<string> { "/Audits/Receipt/pnl-receipt.png" },
                    Items = new List<OcrItemResult>
                    {
                        new OcrItemResult { Name = "San Mig", Quantity = 1, Price = 80m, Total = 80m, PnlCategoryId = 20 },
                        new OcrItemResult { Name = "Gas", Quantity = 1, Price = 100m, Total = 100m, PnlCategoryId = 21 }
                    }
                };

                var submitResult = await auditController.SubmitAudit(model);

                Assert.IsType<RedirectToActionResult>(submitResult);
                var savedAudit = await context.AuditItems
                    .Include(a => a.Details)
                    .SingleAsync(a => a.Description == "Branch categorized receipt");
                Assert.Contains(savedAudit.Details, detail => detail.PnlCategoryId == 20 && detail.PnlSection == PnlExpenseSection.COGS && detail.PnlCategoryName == "Beers");
                Assert.Contains(savedAudit.Details, detail => detail.PnlCategoryId == 21 && detail.PnlSection == PnlExpenseSection.OPEX && detail.PnlCategoryName == "LPG");
            }
        }

        [Fact]
        public async Task Index_ManagerRole_ShowsPendingDailySalesForAssignedBranches()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                context.Users.Add(new User { Id = 7, Name = "Managed Branch Staff", Email = "managed-staff@test.com", PasswordHash = "hash", Role = UserRole.BranchStaff, EstablishmentId = 1, ManagerId = 2 });
                context.DocumentRecords.AddRange(
                    new DocumentRecord { Id = 201, DocumentType = DocumentType.DailySalesReport, UploadedByUserId = 7, ImageUrl = "/sales/main.jpg", OcrStatus = OcrStatus.Parsed, ReviewStatus = DocumentReviewStatus.PendingManagerVerification },
                    new DocumentRecord { Id = 202, DocumentType = DocumentType.DailySalesReport, UploadedByUserId = 6, ImageUrl = "/sales/other.jpg", OcrStatus = OcrStatus.Parsed, ReviewStatus = DocumentReviewStatus.PendingManagerVerification },
                    new DocumentRecord { Id = 203, DocumentType = DocumentType.DailySalesReport, UploadedByUserId = 7, ImageUrl = "/sales/confirmed.jpg", OcrStatus = OcrStatus.Parsed, ReviewStatus = DocumentReviewStatus.Confirmed });
                context.SalesReports.AddRange(
                    new SalesReport { DocumentRecordId = 201, EstablishmentId = 1, CashierName = "Managed Cashier", BusinessDate = new DateTime(2026, 8, 13), HandoverDate = new DateTime(2026, 8, 13), GrossSales = 12000m, ConfirmedCashToHandover = 11000m, Status = SalesReportStatus.PendingManagerVerification },
                    new SalesReport { DocumentRecordId = 202, EstablishmentId = 2, CashierName = "Other Cashier", BusinessDate = new DateTime(2026, 8, 13), HandoverDate = new DateTime(2026, 8, 13), GrossSales = 9000m, ConfirmedCashToHandover = 8500m, Status = SalesReportStatus.PendingManagerVerification },
                    new SalesReport { DocumentRecordId = 203, EstablishmentId = 1, CashierName = "Confirmed Cashier", BusinessDate = new DateTime(2026, 8, 13), HandoverDate = new DateTime(2026, 8, 13), GrossSales = 7000m, ConfirmedCashToHandover = 7000m, Status = SalesReportStatus.Confirmed });
                await context.SaveChangesAsync();

                var homeController = new HomeController(NullLogger<HomeController>.Instance, context)
                {
                    ControllerContext = CreateController(context, 2, "Manager").ControllerContext
                };

                var dashboardResult = await homeController.Index(new DashboardViewModel());
                var viewResult = Assert.IsType<ViewResult>(dashboardResult);
                var dashboard = Assert.IsType<DashboardViewModel>(viewResult.Model);
                var report = Assert.Single(dashboard.PendingSalesReports);
                Assert.Equal("Managed Cashier", report.CashierName);
                Assert.Equal(12000m, dashboard.PendingSalesGrossTotal);
                Assert.Equal(11000m, dashboard.PendingSalesCashToHandoverTotal);
            }
        }

        [Fact]
        public async Task Index_HistoricalData_IncludesFilteredDailySalesReports()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                context.DocumentRecords.AddRange(
                    new DocumentRecord { Id = 301, DocumentType = DocumentType.DailySalesReport, UploadedByUserId = 1, ImageUrl = "/sales/main.jpg", OcrStatus = OcrStatus.Parsed, ReviewStatus = DocumentReviewStatus.Confirmed },
                    new DocumentRecord { Id = 302, DocumentType = DocumentType.DailySalesReport, UploadedByUserId = 1, ImageUrl = "/sales/other.jpg", OcrStatus = OcrStatus.Parsed, ReviewStatus = DocumentReviewStatus.Confirmed });
                context.SalesReports.AddRange(
                    new SalesReport { Id = 31, DocumentRecordId = 301, EstablishmentId = 1, CashierName = "Daniel", BusinessDate = new DateTime(2026, 8, 13), HandoverDate = new DateTime(2026, 8, 13), GrossSales = 2818m, ConfirmedCashToHandover = 2818m, Status = SalesReportStatus.Confirmed },
                    new SalesReport { Id = 32, DocumentRecordId = 302, EstablishmentId = 2, CashierName = "Other", BusinessDate = new DateTime(2026, 8, 13), HandoverDate = new DateTime(2026, 8, 13), GrossSales = 900m, ConfirmedCashToHandover = 900m, Status = SalesReportStatus.Confirmed });
                await context.SaveChangesAsync();

                var homeController = new HomeController(NullLogger<HomeController>.Instance, context)
                {
                    ControllerContext = CreateController(context, 1, "Owner").ControllerContext
                };

                var result = await homeController.Index(new DashboardViewModel
                {
                    StartDate = new DateTime(2026, 8, 13),
                    EndDate = new DateTime(2026, 8, 13),
                    EstablishmentId = 1
                });

                var viewResult = Assert.IsType<ViewResult>(result);
                var dashboard = Assert.IsType<DashboardViewModel>(viewResult.Model);
                var report = Assert.Single(dashboard.HistoricalSalesReports);
                Assert.Equal("Daniel", report.CashierName);
                Assert.Equal(2818m, report.GrossSales);
            }
        }

        [Fact]
        public void Dashboard_HistoricalFilterForm_PreservesHistoricalTab()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "Home", "Index.cshtml"));
            var view = File.ReadAllText(viewPath);

            Assert.Contains("name=\"ActiveTab\" value=\"historical\"", view);
            Assert.Contains("const initialDashboardTab = '@Model.ActiveTab'", view);
            Assert.Contains("data-dashboard-tab=\"historical\"", view);
        }

        [Fact]
        public async Task Index_StatusDropdown_UsesCanonicalAuditStatusesOnly()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var homeController = new HomeController(NullLogger<HomeController>.Instance, context)
                {
                    ControllerContext = CreateController(context, 1, "Owner").ControllerContext
                };

                var result = await homeController.Index(new DashboardViewModel());

                Assert.IsType<ViewResult>(result);
                var statuses = Assert.IsAssignableFrom<IEnumerable<SelectListItem>>((object)homeController.ViewBag.Statuses);
                var labels = statuses.Select(status => status.Text).ToList();

                Assert.Equal(new[]
                {
                    "AwaitingBranchVerification",
                    "AwaitingManagerApproval",
                    "Approved",
                    "Rejected",
                    "Pending",
                    "Cancelled"
                }, labels);
                Assert.DoesNotContain("AwaitingBranchVerifi", labels);
                Assert.DoesNotContain("AwaitingManagerAppro", labels);
            }
        }

        [Fact]
        public async Task Index_RecordTypeAuditsOnly_HidesHistoricalSalesReports()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                context.AuditItems.Add(new AuditItem { Id = 41, BuyerId = 3, EstablishmentId = 1, EntryDate = new DateTime(2026, 8, 13), Amount = 120m, Description = "Audit receipt", Status = AuditStatus.Approved });
                context.DocumentRecords.Add(new DocumentRecord { Id = 303, DocumentType = DocumentType.DailySalesReport, UploadedByUserId = 1, ImageUrl = "/sales/main.jpg", OcrStatus = OcrStatus.Parsed, ReviewStatus = DocumentReviewStatus.Confirmed });
                context.SalesReports.Add(new SalesReport { Id = 33, DocumentRecordId = 303, EstablishmentId = 1, CashierName = "Daniel", BusinessDate = new DateTime(2026, 8, 13), HandoverDate = new DateTime(2026, 8, 13), GrossSales = 2818m, ConfirmedCashToHandover = 2818m, Status = SalesReportStatus.Confirmed });
                await context.SaveChangesAsync();

                var homeController = new HomeController(NullLogger<HomeController>.Instance, context)
                {
                    ControllerContext = CreateController(context, 1, "Owner").ControllerContext
                };

                var result = await homeController.Index(new DashboardViewModel
                {
                    StartDate = new DateTime(2026, 8, 13),
                    EndDate = new DateTime(2026, 8, 13),
                    RecordType = DashboardRecordType.Audits
                });

                var viewResult = Assert.IsType<ViewResult>(result);
                var dashboard = Assert.IsType<DashboardViewModel>(viewResult.Model);
                Assert.Single(dashboard.Audits);
                Assert.Empty(dashboard.HistoricalSalesReports);
            }
        }

        [Fact]
        public async Task Index_RecordTypeDailySalesOnly_HidesHistoricalAudits()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                context.AuditItems.Add(new AuditItem { Id = 42, BuyerId = 3, EstablishmentId = 1, EntryDate = new DateTime(2026, 8, 13), Amount = 120m, Description = "Audit receipt", Status = AuditStatus.Approved });
                context.DocumentRecords.Add(new DocumentRecord { Id = 304, DocumentType = DocumentType.DailySalesReport, UploadedByUserId = 1, ImageUrl = "/sales/main.jpg", OcrStatus = OcrStatus.Parsed, ReviewStatus = DocumentReviewStatus.Confirmed });
                context.SalesReports.Add(new SalesReport { Id = 34, DocumentRecordId = 304, EstablishmentId = 1, CashierName = "Daniel", BusinessDate = new DateTime(2026, 8, 13), HandoverDate = new DateTime(2026, 8, 13), GrossSales = 2818m, ConfirmedCashToHandover = 2818m, Status = SalesReportStatus.Confirmed });
                await context.SaveChangesAsync();

                var homeController = new HomeController(NullLogger<HomeController>.Instance, context)
                {
                    ControllerContext = CreateController(context, 1, "Owner").ControllerContext
                };

                var result = await homeController.Index(new DashboardViewModel
                {
                    StartDate = new DateTime(2026, 8, 13),
                    EndDate = new DateTime(2026, 8, 13),
                    RecordType = DashboardRecordType.DailySales
                });

                var viewResult = Assert.IsType<ViewResult>(result);
                var dashboard = Assert.IsType<DashboardViewModel>(viewResult.Model);
                Assert.Empty(dashboard.Audits);
                Assert.Single(dashboard.HistoricalSalesReports);
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
        public void Review_ViewLimitsPnlCategoriesToBranchStaffAndShowsLineDestinationsForOthers()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "Audits", "Review.cshtml"));
            var view = File.ReadAllText(viewPath);
            var lineItemsStart = view.IndexOf("Extracted Line Items", StringComparison.Ordinal);
            var lineItemsEnd = view.IndexOf("Submit / Batch Buttons", lineItemsStart, StringComparison.Ordinal);
            Assert.Contains("var isBranchStaff = User.IsInRole(\"BranchStaff\")", view);
            var lineItemsMarkup = view.Substring(lineItemsStart, lineItemsEnd - lineItemsStart);

            Assert.Contains("@if (isBranchStaff)", lineItemsMarkup);
            Assert.Contains("Items[i].PnlCategoryId", lineItemsMarkup);
            Assert.Contains("ViewBag.PnlCategories", view);
            Assert.Contains("Items[i].CombinedDestinationId", lineItemsMarkup);
            Assert.Contains("ViewBag.LineDestinations", view);
            Assert.Contains("syncLineDestinationsWithMain", view);
            Assert.Contains("pnl-category-select-template", view);
            Assert.Contains("data-follows-main-allocation", view);
            Assert.Contains("select.dataset.followsMainAllocation !== 'false'", view);
        }

        [Fact]
        public void Migrations_IncludeRepairForPreviouslyAppliedPnlCategorySchema()
        {
            using var context = new AuditDbContext(_options);

            Assert.Contains("20260813123000_RepairAuditPnlCategorySchema", context.Database.GetMigrations());
        }

        [Fact]
        public void Layout_DoesNotShowPcfMonitorInsideBranchStaffNavigation()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "Shared", "_Layout.cshtml"));
            var view = File.ReadAllText(viewPath);
            var branchStart = view.IndexOf("@if (User.IsInRole(\"BranchStaff\"))", StringComparison.Ordinal);
            var branchEnd = view.IndexOf("</nav>", branchStart, StringComparison.Ordinal);
            var branchNavigation = view.Substring(branchStart, branchEnd - branchStart);

            Assert.Contains("Daily Sales", branchNavigation);
            Assert.DoesNotContain("asp-controller=\"Reports\"", branchNavigation);
            Assert.DoesNotContain("PCF Monitor", branchNavigation);
            Assert.DoesNotContain("asp-controller=\"PcfMonitor\"", branchNavigation);
        }

        [Fact]
        public void Layout_ExposesVoiceAssistControlsForOwner()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "Shared", "_Layout.cshtml"));
            var view = File.ReadAllText(viewPath);

            Assert.Contains("User.IsInRole(\"Owner\")", view);
            Assert.Contains("id=\"voiceAssistToggle\"", view);
            Assert.Contains("id=\"globalA11yStatus\"", view);
            Assert.Contains("Skip to main content", view);
            Assert.Contains("id=\"mainContent\"", view);
        }

        [Fact]
        public void Layout_VoiceSummariesExistOnOwnerPages()
        {
            var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views"));
            var pages = new[]
            {
                "Home/Index.cshtml",
                "Audits/VerifyList.cshtml",
                "Audits/SurrenderQueue.cshtml",
                "Reports/Index.cshtml",
                "Treasury/Index.cshtml",
                "SalesReports/Index.cshtml",
                "PcfMonitor/Index.cshtml",
                "Notifications/Index.cshtml"
            };

            foreach (var page in pages)
            {
                var viewPath = Path.Combine(rootPath, page);
                var view = File.ReadAllText(viewPath);
                Assert.Contains("id=\"pageVoiceSummary\"", view);
            }
        }

        [Fact]
        public async Task SubmitSurrender_ManagerCanSurrenderFullAvailableBalanceToSelectedReviewer()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                context.Users.Add(new User { Id = 7, Name = "Dorothy May", Email = "dorothy@test.com", PasswordHash = "hash", Role = UserRole.Manager, PcfBalance = 0m, DailyStartingFloat = 0m });
                await context.SaveChangesAsync();
                var controller = CreateController(context, 2, "Manager");

                var result = await controller.SubmitSurrender(500m, "Manager returned full available PCF", 7);

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal(nameof(AuditsController.Surrender), redirectResult.ActionName);
                var request = Assert.Single(await context.SurrenderRequests.ToListAsync());
                Assert.Equal(2, request.BuyerId);
                Assert.Equal(500m, request.DeclaredAmount);
                Assert.Equal(7, request.AssignedReceiverId);
            }
        }

        [Fact]
        public async Task SubmitSurrender_AllowsBlankOptionalNotes()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 2, "Manager");
                controller.ModelState.AddModelError(nameof(SurrenderRequest.BuyerNotes), "The BuyerNotes field is required.");

                var result = await controller.SubmitSurrender(500m, null, 2);

                var redirectResult = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal(nameof(AuditsController.Surrender), redirectResult.ActionName);
                var request = Assert.Single(await context.SurrenderRequests.ToListAsync());
                Assert.Equal(500m, request.DeclaredAmount);
                Assert.Null(request.BuyerNotes);
            }
        }

        [Fact]
        public async Task SubmitSurrender_InvalidSelectedReviewerShowsReviewerErrorNotAmountError()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateController(context, 2, "Manager");

                var result = await controller.SubmitSurrender(500m, "Manager returned full available PCF", 999);

                Assert.IsType<ViewResult>(result);
                Assert.True(controller.ModelState.ContainsKey("assignedReceiverId"));
                Assert.DoesNotContain(controller.ModelState.Values.SelectMany(entry => entry.Errors), error => error.ErrorMessage.Contains("Invalid surrender amount"));
            }
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
        public void ReviewViewModel_ExpectedCashToHandover_DoesNotSubtractPcfExpenses()
        {
            var model = new SalesReportReviewViewModel
            {
                GrossSales = 10000m,
                CashOut = 1500m,
                GCashAmount = 2000m,
                CreditAmount = 500m,
                OtherPaymentAmount = 250m,
                ConfirmedCashToHandover = 7250m
            };

            Assert.Equal(7250m, model.ExpectedCashToHandover);
            Assert.Equal(0m, model.ShortOverAmount);
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
        public void SalesReport_PersistsLogbookFieldsAndFlexibleLines()
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
                    BusinessDate = new DateTime(2026, 8, 13),
                    HandoverDate = new DateTime(2026, 8, 13),
                    GrossSales = 8935.2m,
                    ClosingGrossSales = 5773m,
                    FoodSales = 5913m,
                    BeerSales = 795m,
                    BeverageSales = 100m,
                    OtherSales = 0m,
                    CashSales = 4788m,
                    SeniorDiscount = 0m,
                    PwdDiscount = 0m,
                    LoyaltyCardDiscount = 0m,
                    GiftVoucherDiscount = 0m,
                    EmployeeTenPercentDiscount = 0m,
                    EmployeeFivePercentDiscount = 0m,
                    EaglesDiscount = 0m,
                    SalesShortageAmount = 0m,
                    SalesShortageReason = null,
                    SalesOverageAmount = 0m,
                    SalesOverageReason = null,
                    RestoPcf = 0m,
                    PcfFromSales = 0m,
                    ChangeAmount = 0m,
                    Status = SalesReportStatus.Draft
                };

                report.Lines.Add(new SalesReportLine
                {
                    LineType = SalesReportLineType.GCash,
                    Amount = 456m,
                    Label = "GCash Line"
                });

                report.Lines.Add(new SalesReportLine
                {
                    LineType = SalesReportLineType.BankTransfer,
                    Amount = 529m,
                    Label = "BDO"
                });

                context.SalesReports.Add(report);
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(options))
            {
                var savedReport = context.SalesReports
                    .Include(r => r.Lines)
                    .Single();

                Assert.Equal(5773m, savedReport.ClosingGrossSales);
                Assert.Equal(5913m, savedReport.FoodSales);
                Assert.Equal(795m, savedReport.BeerSales);
                Assert.Equal(100m, savedReport.BeverageSales);
                Assert.Equal(4788m, savedReport.CashSales);
                
                Assert.Equal(2, savedReport.Lines.Count);
                
                var gcashLine = savedReport.Lines.Single(l => l.LineType == SalesReportLineType.GCash);
                Assert.Equal(456m, gcashLine.Amount);
                Assert.Equal("GCash Line", gcashLine.Label);

                var bankLine = savedReport.Lines.Single(l => l.LineType == SalesReportLineType.BankTransfer);
                Assert.Equal(529m, bankLine.Amount);
                Assert.Equal("BDO", bankLine.Label);
            }
        }

        [Fact]
        public void AuditItemDetail_CanPersistPnlCategoryFields()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new AuditDbContext(options))
            {
                context.Database.EnsureCreated();

                var buyer = new User
                {
                    Name = "Buyer",
                    Email = "audit-pnl-buyer@example.com",
                    PasswordHash = "hash",
                    Role = UserRole.Buyer
                };
                var establishment = new Establishment { Name = "MAIN" };
                context.Users.Add(buyer);
                context.Establishments.Add(establishment);
                context.SaveChanges();

                var audit = new AuditItem
                {
                    BuyerId = buyer.Id,
                    EstablishmentId = establishment.Id,
                    Amount = 1080m,
                    Description = "PCF receipt",
                    EntryDate = new DateTime(2026, 5, 1),
                    Status = AuditStatus.Approved
                };
                audit.Details.Add(new AuditItemDetail
                {
                    ItemName = "San Mig",
                    Quantity = 1,
                    Price = 80m,
                    Total = 80m,
                    AssignedEstablishmentId = establishment.Id,
                    PnlSection = PnlExpenseSection.COGS,
                    PnlCategoryName = "Beer"
                });
                audit.Details.Add(new AuditItemDetail
                {
                    ItemName = "Gas",
                    Quantity = 1,
                    Price = 1000m,
                    Total = 1000m,
                    AssignedEstablishmentId = establishment.Id,
                    PnlSection = PnlExpenseSection.OPEX,
                    PnlCategoryName = "LPG"
                });

                context.AuditItems.Add(audit);
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(options))
            {
                var savedAudit = context.AuditItems
                    .Include(a => a.Details)
                    .Single();

                Assert.Equal(2, savedAudit.Details.Count);
                Assert.Equal(1080m, savedAudit.Details.Sum(line => line.Total));
                Assert.Contains(savedAudit.Details, line => line.PnlSection == PnlExpenseSection.COGS && line.PnlCategoryName == "Beer");
                Assert.Contains(savedAudit.Details, line => line.PnlSection == PnlExpenseSection.OPEX && line.PnlCategoryName == "LPG");
            }
        }

        [Fact]
        public void PnlCategory_CanPersistRegisteredCategories()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new AuditDbContext(options))
            {
                context.Database.EnsureCreated();
                context.PnlCategories.AddRange(
                    new PnlCategory { Name = "Beers", Section = PnlExpenseSection.COGS },
                    new PnlCategory { Name = "Rent", Section = PnlExpenseSection.OPEX });
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(options))
            {
                var categories = context.PnlCategories.OrderBy(category => category.Name).ToList();
                Assert.Equal(2, categories.Count);
                Assert.Contains(categories, category => category.Name == "Beers" && category.Section == PnlExpenseSection.COGS && category.IsActive);
                Assert.Contains(categories, category => category.Name == "Rent" && category.Section == PnlExpenseSection.OPEX && category.IsActive);
            }
        }

        [Fact]
        public void PnlReport_BuildsMonthlyTotalsFromApprovedAuditExpenses()
        {
            var report = PnlReportViewModel.Build(new[]
            {
                new AuditItem
                {
                    EstablishmentId = 1,
                    Establishment = new Establishment { Id = 1, Name = "MAIN" },
                    EntryDate = new DateTime(2026, 5, 1),
                    Status = AuditStatus.Approved,
                    Details =
                    {
                        new AuditItemDetail { PnlSection = PnlExpenseSection.COGS, PnlCategoryName = "Beer", ItemName = "San Mig", Total = 80m },
                        new AuditItemDetail { PnlSection = PnlExpenseSection.COGS, PnlCategoryName = "Beverages", ItemName = "Coke", Total = 200m },
                        new AuditItemDetail { PnlSection = PnlExpenseSection.OPEX, PnlCategoryName = "LPG", ItemName = "Gas", Total = 1000m }
                    }
                },
                new AuditItem
                {
                    EstablishmentId = 2,
                    Establishment = new Establishment { Id = 2, Name = "BRANCH 4" },
                    EntryDate = new DateTime(2026, 5, 2),
                    Status = AuditStatus.Approved,
                    Details =
                    {
                        new AuditItemDetail { PnlSection = PnlExpenseSection.COGS, PnlCategoryName = "Beer", ItemName = "Red Horse", Total = 120m },
                        new AuditItemDetail { PnlSection = PnlExpenseSection.MonthlyFixedCost, PnlCategoryName = "Rent", ItemName = "Rent", Total = 5000m },
                        new AuditItemDetail { PnlSection = PnlExpenseSection.Other, PnlCategoryName = "Other", ItemName = "Cleaning cloths", Total = 50m }
                    }
                },
                new AuditItem
                {
                    EstablishmentId = 1,
                    Establishment = new Establishment { Id = 1, Name = "MAIN" },
                    EntryDate = new DateTime(2026, 5, 3),
                    Status = AuditStatus.AwaitingManagerApproval,
                    Details =
                    {
                        new AuditItemDetail { PnlSection = PnlExpenseSection.COGS, PnlCategoryName = "Beer", ItemName = "Draft item", Total = 999m }
                    }
                }
            }, new[]
            {
                new SalesReport { EstablishmentId = 1, Establishment = new Establishment { Id = 1, Name = "MAIN" }, BusinessDate = new DateTime(2026, 5, 1), GrossSales = 35000m, Status = SalesReportStatus.Confirmed },
                new SalesReport { EstablishmentId = 2, Establishment = new Establishment { Id = 2, Name = "BRANCH 4" }, BusinessDate = new DateTime(2026, 5, 2), GrossSales = 15000m, Status = SalesReportStatus.Confirmed },
                new SalesReport { EstablishmentId = 1, Establishment = new Establishment { Id = 1, Name = "MAIN" }, BusinessDate = new DateTime(2026, 5, 3), GrossSales = 999m, Status = SalesReportStatus.Draft }
            }, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

            Assert.Equal(50000m, report.TotalSales);
            Assert.Equal(400m, report.CogsTotal);
            Assert.Equal(49600m, report.GrossProfit);
            Assert.Equal(1000m, report.OpexTotal);
            Assert.Equal(5000m, report.MonthlyFixedCostTotal);
            Assert.Equal(43550m, report.NetProfit);
            Assert.Equal(87.10m, report.NetProfitPercentage);
            Assert.Contains(report.Categories, category => category.Section == PnlExpenseSection.COGS && category.CategoryName == "Beer" && category.Amount == 200m);
        }

        [Fact]
        public void PnlReport_UsesRegisteredCategorySectionAndName()
        {
            var beers = new PnlCategory { Id = 10, Name = "Beers", Section = PnlExpenseSection.COGS };
            var rent = new PnlCategory { Id = 11, Name = "Rent", Section = PnlExpenseSection.OPEX };

            var report = PnlReportViewModel.Build(new[]
            {
                new AuditItem
                {
                    EstablishmentId = 1,
                    Establishment = new Establishment { Id = 1, Name = "MAIN" },
                    EntryDate = new DateTime(2026, 5, 1),
                    Status = AuditStatus.Approved,
                    Details =
                    {
                        new AuditItemDetail { PnlCategoryId = beers.Id, PnlCategory = beers, ItemName = "San Mig", Total = 80m },
                        new AuditItemDetail { PnlCategoryId = beers.Id, PnlCategory = beers, ItemName = "Redhorse", Total = 120m },
                        new AuditItemDetail { PnlCategoryId = rent.Id, PnlCategory = rent, ItemName = "Store Rent", Total = 5000m },
                        new AuditItemDetail { PnlSection = PnlExpenseSection.OPEX, PnlCategoryName = "Other - OPEX", ItemName = "Unregistered", Total = 50m }
                    }
                }
            }, new[]
            {
                new SalesReport { EstablishmentId = 1, Establishment = new Establishment { Id = 1, Name = "MAIN" }, BusinessDate = new DateTime(2026, 5, 1), GrossSales = 10000m, Status = SalesReportStatus.Confirmed }
            }, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

            Assert.Equal(200m, report.CogsTotal);
            Assert.Equal(5050m, report.OpexTotal);
            Assert.Contains(report.Categories, category => category.Section == PnlExpenseSection.COGS && category.CategoryName == "Beers" && category.Amount == 200m);
            Assert.Contains(report.Categories, category => category.Section == PnlExpenseSection.OPEX && category.CategoryName == "Rent" && category.Amount == 5000m);
            Assert.Contains(report.Categories, category => category.Section == PnlExpenseSection.OPEX && category.CategoryName == "Other - OPEX" && category.Amount == 50m);
        }

        [Fact]
        public void PnlReport_BranchFilterIncludesOnlySelectedBranchAuditDetails()
        {
            var report = PnlReportViewModel.Build(new[]
            {
                new AuditItem
                {
                    EstablishmentId = 1,
                    Establishment = new Establishment { Id = 1, Name = "MAIN" },
                    EntryDate = new DateTime(2026, 5, 1),
                    Status = AuditStatus.Approved,
                    Details =
                    {
                        new AuditItemDetail { AssignedEstablishmentId = 1, PnlSection = PnlExpenseSection.COGS, PnlCategoryName = "Beer", ItemName = "San Mig", Total = 80m },
                        new AuditItemDetail { AssignedEstablishmentId = 2, PnlSection = PnlExpenseSection.OPEX, PnlCategoryName = "LPG", ItemName = "Branch LPG", Total = 1000m }
                    }
                },
                new AuditItem
                {
                    EstablishmentId = 2,
                    Establishment = new Establishment { Id = 2, Name = "BRANCH 4" },
                    EntryDate = new DateTime(2026, 5, 2),
                    Status = AuditStatus.Approved,
                    Details =
                    {
                        new AuditItemDetail { PnlSection = PnlExpenseSection.COGS, PnlCategoryName = "Beer", ItemName = "Branch Beer", Total = 120m }
                    }
                }
            }, new[]
            {
                new SalesReport { EstablishmentId = 2, Establishment = new Establishment { Id = 2, Name = "BRANCH 4" }, BusinessDate = new DateTime(2026, 5, 2), GrossSales = 15000m, Status = SalesReportStatus.Confirmed }
            }, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31), establishmentId: 2);

            Assert.Equal(15000m, report.TotalSales);
            Assert.Equal(120m, report.CogsTotal);
            Assert.Equal(1000m, report.OpexTotal);
            Assert.DoesNotContain(report.Categories, category => category.CategoryName == "Beer" && category.Amount == 200m);
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

        [Fact]
        public void ThirtyDayQaSeed_CreatesCompleteContiguousWorkflowDataset()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);

            DbSeeder.Seed(context, true);

            const string marker = "[seed:thirty-day-qa]";
            var demoSalesReports = context.SalesReports
                .Include(report => report.DocumentRecord)
                .Where(report => report.Notes != null && report.Notes.Contains(marker))
                .ToList();
            var demoAudits = context.AuditItems
                .Include(audit => audit.Details)
                .Where(audit => audit.Notes != null && audit.Notes.Contains(marker))
                .ToList();
            var demoFlows = context.TreasuryCashFlows
                .Include(flow => flow.Entries)
                .Where(flow => flow.Entries.Any(entry => entry.Notes != null && entry.Notes.Contains(marker)))
                .OrderBy(flow => flow.CashFlowDate)
                .ToList();

            Assert.Equal(120, demoSalesReports.Count);
            Assert.Equal(30, demoFlows.Count);
            Assert.Equal(121, demoAudits.Count);
            Assert.Equal(30, demoSalesReports.Select(report => report.BusinessDate.Date).Distinct().Count());
            Assert.Equal(4, demoSalesReports.Select(report => report.EstablishmentId).Distinct().Count());
            Assert.Contains(demoAudits, audit => audit.Status == AuditStatus.Approved);
            Assert.Contains(demoAudits, audit => audit.Status == AuditStatus.Rejected);
            Assert.DoesNotContain(demoAudits, audit => audit.Status == AuditStatus.AwaitingBranchVerification || audit.Status == AuditStatus.AwaitingManagerApproval || audit.Status == AuditStatus.Pending);
            Assert.All(demoAudits.SelectMany(audit => audit.Details), detail => Assert.True(detail.BranchVerificationStatus == BranchVerificationStatus.Verified || detail.BranchVerificationStatus == BranchVerificationStatus.Rejected));
            Assert.Contains(context.PcfReleases, release => release.Purpose != null && release.Purpose.Contains(marker));
            Assert.Contains(context.SurrenderRequests, request => request.BuyerNotes != null && request.BuyerNotes.Contains(marker) && request.Status == SurrenderStatus.Confirmed);
            Assert.Contains(context.AuditSettlements, settlement => settlement.ReceiverName != null && settlement.ReceiverName.Contains(marker));

            for (var index = 1; index < demoFlows.Count; index++)
            {
                Assert.Equal(demoFlows[index - 1].ClosingBalance, demoFlows[index].StartingBalance);
            }

            Assert.All(demoFlows, flow =>
            {
                Assert.Contains(flow.Entries, entry => entry.Direction == CashFlowDirection.In && entry.Category == CashFlowCategory.Sales);
                Assert.Contains(flow.Entries, entry => entry.Direction == CashFlowDirection.Out);
                Assert.Equal(flow.StartingBalance + flow.TotalCashIn, flow.NetCashFlow);
                Assert.Equal(flow.NetCashFlow - flow.TotalCashOut, flow.ClosingBalance);
            });

            var pnl = PnlReportViewModel.Build(
                demoAudits,
                demoSalesReports,
                demoFlows.First().CashFlowDate,
                demoFlows.Last().CashFlowDate);

            Assert.True(pnl.TotalSales > 0m);
            Assert.True(pnl.CogsTotal > 0m);
            Assert.True(pnl.OpexTotal > 0m);
            Assert.True(pnl.NetProfit != 0m);
            Assert.Equal(4, pnl.Branches.Count);
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
                },
                TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
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
        public void Index_ShowsZeroStartingWhenPriorDayNotClosedWithoutCreatingFlow()
        {
            using (var context = new AuditDbContext(_options))
            {
                context.Users.Add(new User { Name = "Treasury Owner", Email = "empty-treasury-owner@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true });
                context.SaveChanges();
                context.TreasuryCashFlows.Add(new TreasuryCashFlow
                {
                    TreasuryUserId = context.Users.Single().Id,
                    CashFlowDate = new DateTime(2026, 8, 9),
                    StartingBalance = 500m,
                    TotalCashIn = 100m,
                    TotalCashOut = 200m,
                    NetCashFlow = 600m,
                    ClosingBalance = 400m,
                    Status = TreasuryCashFlowStatus.Open
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

        [Fact]
        public void Index_CarriesForwardClosingFromClosedPriorDayWithoutCreatingFlow()
        {
            using (var context = new AuditDbContext(_options))
            {
                context.Users.Add(new User { Name = "Treasury Owner", Email = "carried-owner@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true });
                context.SaveChanges();
                context.TreasuryCashFlows.Add(new TreasuryCashFlow
                {
                    TreasuryUserId = context.Users.Single().Id,
                    CashFlowDate = new DateTime(2026, 8, 9),
                    StartingBalance = 500m,
                    TotalCashIn = 100m,
                    TotalCashOut = 200m,
                    NetCashFlow = 600m,
                    ClosingBalance = 400m,
                    Status = TreasuryCashFlowStatus.Closed
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

                Assert.Null(model.FlowId);
                Assert.Empty(model.Entries);
                Assert.Equal(400m, model.StartingBalance);
                Assert.Equal(0m, model.TotalCashOut);
                Assert.Equal(cashFlowCountBefore, context.TreasuryCashFlows.Count());
            }
        }

        [Fact]
        public void Index_DoesNotCarryAcrossGapWhenImmediatePreviousDayHasNoFlow()
        {
            using (var context = new AuditDbContext(_options))
            {
                context.Users.Add(new User { Name = "Treasury Owner", Email = "carried-gap-owner@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true });
                context.SaveChanges();
                context.TreasuryCashFlows.Add(new TreasuryCashFlow
                {
                    TreasuryUserId = context.Users.Single().Id,
                    CashFlowDate = new DateTime(2026, 8, 9),
                    StartingBalance = 500m,
                    TotalCashIn = 100m,
                    TotalCashOut = 200m,
                    NetCashFlow = 600m,
                    ClosingBalance = 400m,
                    Status = TreasuryCashFlowStatus.Closed
                });
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = CreateController(context);
                var result = controller.Index(new DateTime(2026, 8, 11));

                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<TreasuryCashFlowViewModel>(viewResult.Model);

                Assert.Null(model.FlowId);
                Assert.Equal(0m, model.StartingBalance);
            }
        }

        [Fact]
        public async Task CloseTreasury_MarksOpenFlowAsClosedAndRecomputesTotals()
        {
            using (var context = new AuditDbContext(_options))
            {
                var user = new User { Name = "Treasury Owner", Email = "close-treasury@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true };
                context.Users.Add(user);
                context.SaveChanges();

                var flow = new TreasuryCashFlow
                {
                    TreasuryUserId = user.Id,
                    CashFlowDate = new DateTime(2026, 8, 20),
                    StartingBalance = 1000m,
                    Status = TreasuryCashFlowStatus.Open
                };
                flow.Entries.Add(new CashFlowEntry { Direction = CashFlowDirection.In, Category = CashFlowCategory.Sales, Amount = 500m, CreatedByUserId = user.Id, ConfirmedByUserId = user.Id });
                flow.Entries.Add(new CashFlowEntry { Direction = CashFlowDirection.Out, Category = CashFlowCategory.Expense, Amount = 200m, CreatedByUserId = user.Id, ConfirmedByUserId = user.Id });
                context.TreasuryCashFlows.Add(flow);
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = CreateController(context);
                var result = await controller.CloseTreasury(new DateTime(2026, 8, 20));

                Assert.IsType<RedirectToActionResult>(result);

                var saved = context.TreasuryCashFlows.Single(f => f.CashFlowDate.Date == new DateTime(2026, 8, 20));
                Assert.Equal(TreasuryCashFlowStatus.Closed, saved.Status);
                Assert.Equal(500m, saved.TotalCashIn);
                Assert.Equal(200m, saved.TotalCashOut);
                Assert.Equal(1500m, saved.NetCashFlow);
                Assert.Equal(1300m, saved.ClosingBalance);
            }
        }

        [Fact]
        public async Task CloseTreasury_DoesNotCreateFlowAndRedirectsWhenNoneExists()
        {
            using (var context = new AuditDbContext(_options))
            {
                var user = new User { Name = "Treasury Owner", Email = "close-treasury-empty@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true };
                context.Users.Add(user);
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = CreateController(context);
                var countBefore = context.TreasuryCashFlows.Count();
                var result = await controller.CloseTreasury(new DateTime(2026, 8, 22));

                Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal(countBefore, context.TreasuryCashFlows.Count());
            }
        }

        [Fact]
        public async Task CloseTreasury_DoesNotRecloseWhenAlreadyClosed()
        {
            using (var context = new AuditDbContext(_options))
            {
                context.Users.Add(new User { Name = "Treasury Owner", Email = "close-treasury-reclose@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true });
                context.SaveChanges();
                context.TreasuryCashFlows.Add(new TreasuryCashFlow
                {
                    TreasuryUserId = context.Users.Single().Id,
                    CashFlowDate = new DateTime(2026, 8, 21),
                    StartingBalance = 50m,
                    Status = TreasuryCashFlowStatus.Closed
                });
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = CreateController(context);
                var result = await controller.CloseTreasury(new DateTime(2026, 8, 21));

                Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal(TreasuryCashFlowStatus.Closed, context.TreasuryCashFlows.Single().Status);
            }
        }

        [Fact]
        public async Task RecordCashIn_SavesManualCashInEntry()
        {
            var cashInDate = new DateTime(2026, 8, 12);
            using (var context = new AuditDbContext(_options))
            {
                context.Users.Add(new User { Id = 1, Name = "Treasury Owner", Email = "cashin-owner@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true });
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = CreateController(context);
                var model = new ManualCashInViewModel
                {
                    CashInDate = cashInDate,
                    Category = CashFlowCategory.Sales,
                    Amount = 1500m,
                    Purpose = "Store Sales August 12"
                };

                var result = await controller.RecordCashIn(model);
                Assert.IsType<RedirectToActionResult>(result);

                var flow = context.TreasuryCashFlows.Include(f => f.Entries).Single(f => f.CashFlowDate == cashInDate.Date);
                var entry = Assert.Single(flow.Entries);
                Assert.Equal(CashFlowDirection.In, entry.Direction);
                Assert.Equal(CashFlowCategory.Sales, entry.Category);
                Assert.Equal("Store Sales August 12", entry.Notes);
                Assert.Equal(1500m, flow.TotalCashIn);
                Assert.Equal(1500m, flow.ClosingBalance);
            }
        }

        [Fact]
        public async Task RecordCashOut_SavesManualCashOutEntry_OptionalEstablishment()
        {
            var cashOutDate = new DateTime(2026, 8, 13);
            using (var context = new AuditDbContext(_options))
            {
                context.Users.Add(new User { Id = 1, Name = "Treasury Owner", Email = "cashout-owner@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true });
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = CreateController(context);
                var model = new ManualCashOutViewModel
                {
                    CashOutDate = cashOutDate,
                    Category = CashFlowCategory.Others,
                    Amount = 850m,
                    Purpose = "Gas replenishment",
                    EstablishmentId = null // Optional establishment!
                };

                var result = await controller.RecordCashOut(model);
                Assert.IsType<RedirectToActionResult>(result);

                var flow = context.TreasuryCashFlows.Include(f => f.Entries).Single(f => f.CashFlowDate == cashOutDate.Date);
                var entry = Assert.Single(flow.Entries);
                Assert.Equal(CashFlowDirection.Out, entry.Direction);
                Assert.Equal(CashFlowCategory.Others, entry.Category);
                Assert.Null(entry.EstablishmentId);
                Assert.Equal("Gas replenishment", entry.Notes);
                Assert.Equal(850m, flow.TotalCashOut);
                Assert.Equal(-850m, flow.ClosingBalance);
            }
        }

        [Fact]
        public async Task RecordCashOut_AppliesAcrossEstablishmentsSavesOneGeneralEntry()
        {
            var cashOutDate = new DateTime(2026, 8, 14);
            using (var context = new AuditDbContext(_options))
            {
                context.Users.Add(new User { Id = 1, Name = "Treasury Owner", Email = "across-owner@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true });
                context.Establishments.AddRange(
                    new Establishment { Id = 20, Name = "MAIN Branch", IsOperatingBranch = true, IsActive = true },
                    new Establishment { Id = 21, Name = "B4 Branch", IsOperatingBranch = true, IsActive = true }
                );
                context.SaveChanges();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = CreateController(context);
                var model = new ManualCashOutViewModel
                {
                    CashOutDate = cashOutDate,
                    Category = CashFlowCategory.Others,
                    Amount = 500m,
                    Purpose = "Shared grocery",
                    EstablishmentId = 20,
                    AppliesAcrossEstablishments = true
                };

                var result = await controller.RecordCashOut(model);
                Assert.IsType<RedirectToActionResult>(result);

                var flow = context.TreasuryCashFlows.Include(f => f.Entries).Single(f => f.CashFlowDate == cashOutDate.Date);
                var entry = Assert.Single(flow.Entries);
                Assert.Equal(CashFlowDirection.Out, entry.Direction);
                Assert.Equal(CashFlowCategory.Others, entry.Category);
                Assert.Null(entry.EstablishmentId);
                Assert.Equal(500m, entry.Amount);
                Assert.Equal("Shared grocery", entry.Notes);
                Assert.Equal(500m, flow.TotalCashOut);
                Assert.Equal(-500m, flow.ClosingBalance);
            }
        }

        [Fact]
        public void TreasuryIndex_ExplainsOptionalEstablishmentAndAcrossEstablishments()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "Treasury", "Index.cshtml"));
            var view = File.ReadAllText(viewPath);

            Assert.Contains("Leave as none for personal, treasury-wide, or non-branch cash-outs.", view);
            Assert.Contains("Applies across establishments", view);
            Assert.Contains("This one amount applies across establishments and is not split.", view);
            Assert.DoesNotContain("ManualCashOut.SplitRows", view);
            Assert.DoesNotContain("manual-cashout-split-rows", view);
        }

        private static TreasuryController CreateControllerForUser(AuditDbContext context, int userId, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
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

        [Fact]
        public async Task RecordCashIn_CreatesSeparateManagerTreasuryRecordsOnSameDate()
        {
            var cashInDate = new DateTime(2026, 8, 14);
            using (var context = new AuditDbContext(_options))
            {
                context.Users.AddRange(
                    new User { Id = 10, Name = "Manager One", Email = "m1@test.com", PasswordHash = "hash", Role = UserRole.Manager, IsTreasury = true },
                    new User { Id = 20, Name = "Manager Two", Email = "m2@test.com", PasswordHash = "hash", Role = UserRole.Manager, IsTreasury = true }
                );
                await context.SaveChangesAsync();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller1 = CreateControllerForUser(context, 10, "Manager");
                var model1 = new ManualCashInViewModel
                {
                    CashInDate = cashInDate,
                    Category = CashFlowCategory.Others,
                    Amount = 100m,
                    Purpose = "Manager One replenishment"
                };
                await controller1.RecordCashIn(model1);

                var controller2 = CreateControllerForUser(context, 20, "Manager");
                var model2 = new ManualCashInViewModel
                {
                    CashInDate = cashInDate,
                    Category = CashFlowCategory.Others,
                    Amount = 200m,
                    Purpose = "Manager Two replenishment"
                };
                await controller2.RecordCashIn(model2);
            }

            using (var context = new AuditDbContext(_options))
            {
                var flows = await context.TreasuryCashFlows
                    .AsNoTracking()
                    .Where(f => f.CashFlowDate == cashInDate.Date)
                    .ToListAsync();

                Assert.Equal(2, flows.Count);
                Assert.Contains(flows, f => f.TreasuryUserId == 10 && f.TotalCashIn == 100m);
                Assert.Contains(flows, f => f.TreasuryUserId == 20 && f.TotalCashIn == 200m);
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
                },
                new User
                {
                    Id = 4,
                    Name = "Deleted Receiver",
                    Email = "pcf-release-deleted-receiver@test.com",
                    PasswordHash = "hash",
                    Role = UserRole.BranchStaff,
                    EstablishmentId = 1,
                    IsDeleted = true
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
            DateTime expectedToday;
            try
            {
                expectedToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila")).Date;
            }
            catch (TimeZoneNotFoundException)
            {
                expectedToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time")).Date;
            }
            Assert.Equal(expectedToday, model.ReleaseDate.Date);

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

        [Fact]
        public async Task ReleasePcf_PostRequiresReceiverContextWithoutSaving()
        {
            using var context = new AuditDbContext(_options);
            await SeedReleaseLookupsAsync(context);
            var controller = CreateController(context);
            var model = new PcfReleaseViewModel
            {
                ReleaseDate = new DateTime(2026, 8, 16),
                Amount = 50m
            };

            var result = await controller.ReleasePcf(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(controller.ModelState[nameof(PcfReleaseViewModel.ReceiverName)]!.Errors, error => error.ErrorMessage.Contains("receiver", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(context.PcfReleases);
            Assert.Empty(context.TreasuryCashFlows);
            Assert.Empty(context.CashFlowEntries);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(99)]
        public async Task ReleasePcf_PostInvalidReceiverUserIdReturnsModelStateErrorWithoutSaving(int receiverUserId)
        {
            using var context = new AuditDbContext(_options);
            await SeedReleaseLookupsAsync(context);
            var controller = CreateController(context);
            var model = new PcfReleaseViewModel
            {
                ReleaseDate = new DateTime(2026, 8, 17),
                Amount = 50m,
                ReceiverUserId = receiverUserId,
                EstablishmentId = 1
            };

            var result = await controller.ReleasePcf(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(controller.ModelState[nameof(PcfReleaseViewModel.ReceiverUserId)]!.Errors, error => error.ErrorMessage.Contains("active", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(context.PcfReleases);
            Assert.Empty(context.TreasuryCashFlows);
            Assert.Empty(context.CashFlowEntries);
        }
    }

    public class AuditSettlementUsabilityTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public AuditSettlementUsabilityTests()
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

        private static TreasuryController CreateController(AuditDbContext context, int currentUserId = 1, string currentUserRole = "Owner")
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

            return new TreasuryController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
            };
        }

        private static async Task SeedSettlementLookupsAsync(AuditDbContext context)
        {
            context.Users.AddRange(
                new User
                {
                    Id = 1,
                    Name = "Treasury Owner",
                    Email = "settlement-owner@test.com",
                    PasswordHash = "hash",
                    Role = UserRole.Owner,
                    IsTreasury = true
                },
                new User
                {
                    Id = 2,
                    Name = "Active Manager",
                    Email = "settlement-manager@test.com",
                    PasswordHash = "hash",
                    Role = UserRole.Manager
                },
                new User
                {
                    Id = 3,
                    Name = "Deleted Manager",
                    Email = "settlement-deleted-manager@test.com",
                    PasswordHash = "hash",
                    Role = UserRole.Manager,
                    IsDeleted = true
                });

            context.PcfReleases.AddRange(
                new PcfRelease
                {
                    Id = 10,
                    ReleasedByTreasuryUserId = 1,
                    ReceiverName = "Branch Receiver",
                    Amount = 500m,
                    ReleaseDate = new DateTime(2026, 8, 10),
                    Status = PcfReleaseStatus.Released
                },
                new PcfRelease
                {
                    Id = 11,
                    ReleasedByTreasuryUserId = 1,
                    ReceiverName = "Settled Receiver",
                    Amount = 250m,
                    ReleaseDate = new DateTime(2026, 8, 9),
                    Status = PcfReleaseStatus.Settled
                });

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task Settlement_GetLoadsViewModelManagersAndAvailablePcfReleases()
        {
            using var context = new AuditDbContext(_options);
            await SeedSettlementLookupsAsync(context);
            var controller = CreateController(context);

            var result = await controller.Settlement();

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<AuditSettlementViewModel>(viewResult.Model);

            var managers = Assert.IsAssignableFrom<SelectList>((object)controller.ViewBag.ResponsibleManagers);
            Assert.Contains(managers, item => item.Value == "2" && item.Text == "Active Manager");
            Assert.DoesNotContain(managers, item => item.Value == "3");

            var releases = Assert.IsAssignableFrom<SelectList>((object)controller.ViewBag.PcfReleases);
            Assert.Contains(releases, item => item.Value == "10");
            Assert.DoesNotContain(releases, item => item.Value == "11");
        }

        [Fact]
        public async Task Settlement_PostCreatesConfirmedSettlementWithSelectedManagerRecomputedAmountsAndPcfLink()
        {
            using var context = new AuditDbContext(_options);
            await SeedSettlementLookupsAsync(context);
            var controller = CreateController(context, currentUserId: 1, currentUserRole: "Owner");
            var model = new AuditSettlementViewModel
            {
                PcfReleaseId = 10,
                ResponsibleManagerId = 2,
                ReceiverName = "  Settlement Receiver  ",
                TotalPCReleased = 500m,
                TotalAcceptedExpenses = 375.25m,
                ActualChangeReturned = 100m
            };

            var result = await controller.Settlement(model);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(TreasuryController.Settlement), redirect.ActionName);
            Assert.Equal("Audit settlement saved.", controller.TempData["Message"]);

            var settlement = await context.AuditSettlements.AsNoTracking().SingleAsync();
            Assert.Equal(10, settlement.PcfReleaseId);
            Assert.Equal(1, settlement.ProcessedByUserId);
            Assert.Equal(2, settlement.ResponsibleManagerId);
            Assert.Equal("Settlement Receiver", settlement.ReceiverName);
            Assert.Equal(500m, settlement.TotalPCReleased);
            Assert.Equal(375.25m, settlement.TotalAcceptedExpenses);
            Assert.Equal(124.75m, settlement.ExpectedChange);
            Assert.Equal(100m, settlement.ActualChangeReturned);
            Assert.Equal(-24.75m, settlement.ShortOverAmount);
            Assert.Equal(AuditSettlementStatus.Confirmed, settlement.Status);
        }

        [Theory]
        [InlineData("Manager")]
        [InlineData("Owner")]
        [InlineData("Admin")]
        public async Task Settlement_PostFallsBackToCurrentPrivilegedUserWhenNoManagerSelected(string role)
        {
            using var context = new AuditDbContext(_options);
            await SeedSettlementLookupsAsync(context);
            var controller = CreateController(context, currentUserId: 1, currentUserRole: role);

            var result = await controller.Settlement(new AuditSettlementViewModel
            {
                TotalPCReleased = 100m,
                TotalAcceptedExpenses = 80m,
                ActualChangeReturned = 20m
            });

            Assert.IsType<RedirectToActionResult>(result);
            var settlement = await context.AuditSettlements.AsNoTracking().SingleAsync();
            Assert.Equal(1, settlement.ResponsibleManagerId);
            Assert.Equal(1, settlement.ProcessedByUserId);
        }

        [Fact]
        public async Task Settlement_PostMissingCurrentUserDoesNotSave()
        {
            using var context = new AuditDbContext(_options);
            await SeedSettlementLookupsAsync(context);
            var controller = CreateController(context, currentUserId: 99, currentUserRole: "Owner");

            var result = await controller.Settlement(new AuditSettlementViewModel
            {
                ResponsibleManagerId = 2,
                TotalPCReleased = 100m,
                TotalAcceptedExpenses = 75m,
                ActualChangeReturned = 25m
            });

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(context.AuditSettlements);
        }

        [Fact]
        public async Task Settlement_PostDeletedCurrentUserDoesNotSave()
        {
            using var context = new AuditDbContext(_options);
            await SeedSettlementLookupsAsync(context);
            var controller = CreateController(context, currentUserId: 3, currentUserRole: "Manager");

            var result = await controller.Settlement(new AuditSettlementViewModel
            {
                TotalPCReleased = 100m,
                TotalAcceptedExpenses = 75m,
                ActualChangeReturned = 25m
            });

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(context.AuditSettlements);
        }

        [Fact]
        public async Task Settlement_PostNegativeAmountsDoNotSaveAndRepopulateLookups()
        {
            using var context = new AuditDbContext(_options);
            await SeedSettlementLookupsAsync(context);
            var controller = CreateController(context);

            var result = await controller.Settlement(new AuditSettlementViewModel
            {
                ResponsibleManagerId = 2,
                TotalPCReleased = -1m,
                TotalAcceptedExpenses = -2m,
                ActualChangeReturned = -3m
            });

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<AuditSettlementViewModel>(viewResult.Model);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey(nameof(AuditSettlementViewModel.TotalPCReleased)));
            Assert.True(controller.ModelState.ContainsKey(nameof(AuditSettlementViewModel.TotalAcceptedExpenses)));
            Assert.True(controller.ModelState.ContainsKey(nameof(AuditSettlementViewModel.ActualChangeReturned)));
            Assert.Empty(context.AuditSettlements);
            Assert.IsAssignableFrom<SelectList>(controller.ViewBag.ResponsibleManagers);
            Assert.IsAssignableFrom<SelectList>(controller.ViewBag.PcfReleases);
        }

        [Theory]
        [InlineData(PcfReleaseStatus.Settled)]
        [InlineData(PcfReleaseStatus.Cancelled)]
        public async Task Settlement_PostRejectsUnavailablePcfReleaseStatusesWithoutSaving(PcfReleaseStatus status)
        {
            using var context = new AuditDbContext(_options);
            await SeedSettlementLookupsAsync(context);
            var release = new PcfRelease
            {
                Id = 20,
                ReleasedByTreasuryUserId = 1,
                ReceiverName = "Unavailable Receiver",
                Amount = 300m,
                ReleaseDate = new DateTime(2026, 8, 8),
                Status = status
            };
            context.PcfReleases.Add(release);
            await context.SaveChangesAsync();
            var controller = CreateController(context);

            var result = await controller.Settlement(new AuditSettlementViewModel
            {
                PcfReleaseId = release.Id,
                ResponsibleManagerId = 2,
                TotalPCReleased = 300m,
                TotalAcceptedExpenses = 250m,
                ActualChangeReturned = 50m
            });

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<AuditSettlementViewModel>(viewResult.Model);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(controller.ModelState[nameof(AuditSettlementViewModel.PcfReleaseId)]!.Errors, error => error.ErrorMessage.Contains("valid PCF release", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(await context.AuditSettlements.AsNoTracking().ToListAsync(), settlement => settlement.PcfReleaseId == release.Id);
            Assert.IsAssignableFrom<SelectList>(controller.ViewBag.PcfReleases);
        }

        [Fact]
        public async Task Settlement_PostRejectsAlreadyLinkedPcfReleaseWithoutSavingDuplicate()
        {
            using var context = new AuditDbContext(_options);
            await SeedSettlementLookupsAsync(context);
            var release = new PcfRelease
            {
                Id = 21,
                ReleasedByTreasuryUserId = 1,
                ReceiverName = "Linked Receiver",
                Amount = 400m,
                ReleaseDate = new DateTime(2026, 8, 7),
                Status = PcfReleaseStatus.Released
            };
            context.PcfReleases.Add(release);
            context.AuditSettlements.Add(new AuditSettlement
            {
                PcfReleaseId = release.Id,
                ResponsibleManagerId = 2,
                ProcessedByUserId = 1,
                TotalPCReleased = 400m,
                TotalAcceptedExpenses = 300m,
                ActualChangeReturned = 100m,
                ExpectedChange = 100m,
                ShortOverAmount = 0m,
                Status = AuditSettlementStatus.Confirmed
            });
            await context.SaveChangesAsync();
            var controller = CreateController(context);

            var result = await controller.Settlement(new AuditSettlementViewModel
            {
                PcfReleaseId = release.Id,
                ResponsibleManagerId = 2,
                TotalPCReleased = 400m,
                TotalAcceptedExpenses = 300m,
                ActualChangeReturned = 100m
            });

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<AuditSettlementViewModel>(viewResult.Model);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(controller.ModelState[nameof(AuditSettlementViewModel.PcfReleaseId)]!.Errors, error => error.ErrorMessage.Contains("valid PCF release", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1, await context.AuditSettlements.CountAsync(settlement => settlement.PcfReleaseId == release.Id));
            Assert.IsAssignableFrom<SelectList>(controller.ViewBag.PcfReleases);
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

        private static async Task<SalesReport> SeedDraftSalesReportAsync(AuditDbContext context, string fileName = "sales-report.jpg")
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
                ImageUrl = $"/SalesReports/Image/{fileName}",
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
        public void SalesReportReview_HidesShortOverNoticeWhenUserCannotConfirmToTreasury()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "SalesReports", "Review.cshtml"));
            var view = File.ReadAllText(viewPath);
            var guardIndex = view.IndexOf("@if (Model.CanConfirmToTreasury)", StringComparison.Ordinal);
            var noticeIndex = view.IndexOf("id=\"short-over-card\"", StringComparison.Ordinal);

            Assert.True(guardIndex >= 0);
            Assert.True(noticeIndex > guardIndex);
        }

        [Fact]
        public void SalesReportReview_ShowsUploadMetadataAsEditableFields()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "SalesReports", "Review.cshtml"));
            var view = File.ReadAllText(viewPath);

            Assert.Contains("Upload Daily Sales", view);
            Assert.Contains("<select asp-for=\"EstablishmentId\"", view);
            Assert.Contains("<input asp-for=\"BusinessDate\"", view);
            Assert.Contains("<input asp-for=\"HandoverDate\"", view);
            Assert.Contains("<input asp-for=\"CashierName\"", view);
            Assert.DoesNotContain("type=\"hidden\" asp-for=\"EstablishmentId\"", view);
            Assert.DoesNotContain("type=\"hidden\" asp-for=\"BusinessDate\"", view);
            Assert.DoesNotContain("type=\"hidden\" asp-for=\"HandoverDate\"", view);
            Assert.DoesNotContain("type=\"hidden\" asp-for=\"CashierName\"", view);
        }

        private static string CreateSalesReportImageFile(string fileName)
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads", "sales-reports");
            Directory.CreateDirectory(folder);
            var filePath = Path.Combine(folder, fileName);
            File.WriteAllBytes(filePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
            return filePath;
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
        public async Task Review_ConfirmWithShortOverNotifiesUploadingBranchStaff()
        {
            using var context = new AuditDbContext(_options);
            var report = await SeedDraftSalesReportAsync(context);
            var controller = CreateController(context, 1, "Manager");
            var model = BuildReviewModel(report, 850m);
            model.GrossSales = 1000m;
            model.CashOut = 100m;
            model.GCashAmount = 0m;
            model.CreditAmount = 0m;
            model.OtherPaymentAmount = 0m;

            var result = await controller.Review(model, "Confirm");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SalesReportsController.Review), redirect.ActionName);
            var notification = await context.Notifications.AsNoTracking().SingleAsync(n => n.UserId == 2);
            Assert.Equal("Daily Sales Short/Over Notice", notification.Title);
            Assert.Equal("SalesReportShortOver", notification.Category);
            Assert.Equal("/SalesReports/Review/1", notification.LinkUrl);
            Assert.Contains("SHORT", notification.Message);
            Assert.Contains("₱150.00", notification.Message);
            Assert.Contains("CKR Sales Branch", notification.Message);
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
        public async Task Review_SubmitForVerification_SavesBranchStaffLogbookDetails()
        {
            using var context = new AuditDbContext(_options);
            var report = await SeedDraftSalesReportAsync(context);
            report.OpeningGrossSales = 500m;
            report.OpeningCashSales = 100m;
            await context.SaveChangesAsync();
            var controller = CreateController(context, 2, "BranchStaff"); // Staff member has access to branch 1

            var model = BuildReviewModel(report, 4788m); // Cash Sales 4788m
            model.ClosingGrossSales = 5773m;
            model.FoodSales = 5913m;
            model.BeerSales = 795m;
            model.BeverageSales = 100m;
            model.OtherSales = 0m;
            model.CashSales = 4788m;
            model.SeniorDiscount = 0m;
            model.PwdDiscount = 0m;
            model.LoyaltyCardDiscount = 0m;
            model.GiftVoucherDiscount = 0m;
            model.EmployeeTenPercentDiscount = 0m;
            model.EmployeeFivePercentDiscount = 0m;
            model.EaglesDiscount = 0m;
            model.SalesShortageAmount = 0m;
            model.SalesShortageReason = null;
            model.SalesOverageAmount = 0m;
            model.SalesOverageReason = null;
            model.RestoPcf = 0m;
            model.PcfFromSales = 0m;
            model.ChangeAmount = 0m;

            // Setup payments
            model.GCashLines.Add(new SalesReportLineViewModel { LineType = SalesReportLineType.GCash, Amount = 456m, Label = "GCash Line" });
            model.BankTransferLines.Add(new SalesReportLineViewModel { LineType = SalesReportLineType.BankTransfer, Amount = 529m, Label = "BDO" });

            // Setup expenses
            model.ExpenseFromSalesLines.Add(new SalesReportLineViewModel { LineType = SalesReportLineType.ExpenseFromSales, Amount = 120m, Label = "rice" });

            var result = await controller.Review(model, "SubmitForVerification");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SalesReportsController.Review), redirect.ActionName);

            var savedReport = await context.SalesReports
                .Include(r => r.Lines)
                .SingleAsync();

            Assert.Equal(SalesReportStatus.PendingManagerVerification, savedReport.Status);
            Assert.Equal(5773m, savedReport.ClosingGrossSales);
            Assert.Equal(5913m, savedReport.FoodSales);
            Assert.Equal(795m, savedReport.BeerSales);
            Assert.Equal(100m, savedReport.BeverageSales);
            Assert.Equal(4788m, savedReport.CashSales);
            
            // Check dynamic lines saved correctly
            Assert.Equal(3, savedReport.Lines.Count);
            Assert.Contains(savedReport.Lines, l => l.LineType == SalesReportLineType.GCash && l.Amount == 456m);
            Assert.Contains(savedReport.Lines, l => l.LineType == SalesReportLineType.BankTransfer && l.Amount == 529m && l.Label == "BDO");
            Assert.Contains(savedReport.Lines, l => l.LineType == SalesReportLineType.ExpenseFromSales && l.Amount == 120m && l.Label == "rice");

            // Check compatibility fields were updated automatically from the list totals
            Assert.Equal(456m, savedReport.GCashAmount);
            Assert.Equal(529m, savedReport.OtherPaymentAmount); // Check BDO maps to OtherPayment
            Assert.Equal(120m, savedReport.CashOut); // Check expenses map to CashOut (PCF Expenses)
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
        public void Upload_ViewLocksAssignedBranchForBranchStaff()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "SalesReports", "Upload.cshtml"));
            var view = File.ReadAllText(viewPath);

            Assert.Contains("User.IsInRole(\"BranchStaff\")", view);
            Assert.Contains("name=\"establishmentId\"", view);
            Assert.Contains("type=\"hidden\"", view);
            Assert.Contains("Assigned operating branch", view);
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
        public async Task Review_ForManagerWithoutReportingBranchStaffIsForbidden()
        {
            using var context = new AuditDbContext(_options);
            var report = await SeedDraftSalesReportAsync(context);
            
            var manager = new User
            {
                Id = 5,
                Name = "Other Manager",
                Email = "other-manager@test.com",
                PasswordHash = "hash",
                Role = UserRole.Manager
            };
            context.Users.Add(manager);
            await context.SaveChangesAsync();

            var staff = await context.Users.FindAsync(2);
            staff.ManagerId = 5;
            await context.SaveChangesAsync();

            var controller = CreateController(context, 4, "Manager");
            var model = BuildReviewModel(report, 1500m);

            var getResult = await controller.Review(report.Id);
            var postResult = await controller.Review(model, "Confirm");

            Assert.IsType<ForbidResult>(getResult);
            Assert.IsType<ForbidResult>(postResult);
        }

        [Fact]
        public async Task Index_ForManagerFiltersPendingReportsByAssignedBranchStaff()
        {
            using var context = new AuditDbContext(_options);
            var report = await SeedDraftSalesReportAsync(context);
            
            report.Status = SalesReportStatus.PendingManagerVerification;
            await context.SaveChangesAsync();

            var manager = new User
            {
                Id = 4,
                Name = "Unrelated Manager",
                Email = "unrelated-manager@test.com",
                PasswordHash = "hash",
                Role = UserRole.Manager
            };
            context.Users.Add(manager);
            await context.SaveChangesAsync();

            var controller = CreateController(context, 4, "Manager");
            var result = await controller.Index();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<SalesReport>>(viewResult.Model);
            Assert.Empty(model);
        }

        [Fact]
        public async Task Image_ForBranchStaffOutsideAssignedBranchIsForbidden()
        {
            using var context = new AuditDbContext(_options);
            var fileName = $"sales-report-scope-{Guid.NewGuid():N}.jpg";
            var filePath = CreateSalesReportImageFile(fileName);
            try
            {
                await SeedDraftSalesReportAsync(context, fileName);
                var controller = CreateController(context, 3, "BranchStaff");

                var result = controller.Image(fileName);

                Assert.IsType<ForbidResult>(result);
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        [Theory]
        [InlineData(1, "Owner")]
        [InlineData(1, "Manager")]
        [InlineData(1, "Admin")]
        [InlineData(2, "BranchStaff")]
        public async Task Image_ForPrivilegedUsersAndAssignedBranchStaffServesSalesReportImage(int currentUserId, string currentUserRole)
        {
            using var context = new AuditDbContext(_options);
            var fileName = $"sales-report-allowed-{Guid.NewGuid():N}.jpg";
            var filePath = CreateSalesReportImageFile(fileName);
            try
            {
                await SeedDraftSalesReportAsync(context, fileName);
                var controller = CreateController(context, currentUserId, currentUserRole);

                var result = controller.Image(fileName);

                var fileResult = Assert.IsType<PhysicalFileResult>(result);
                Assert.Equal(filePath, fileResult.FileName);
                Assert.Equal("image/jpeg", fileResult.ContentType);
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

        private static IFormFile CreateMockFormFile(string filename, string content)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "reportImages", filename)
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/jpeg"
            };
        }

        [Fact]
        public async Task Upload_PostWithNoImages_ReturnsViewAndAddsModelError()
        {
            using var context = new AuditDbContext(_options);
            await SeedDraftSalesReportAsync(context);
            var controller = CreateController(context);
            
            var result = await controller.Upload(1, DateTime.Today, DateTime.Today, "Cashier", new List<IFormFile>());
            
            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(controller.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("at least one", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Upload_PostWithTooManyImages_ReturnsViewAndAddsModelError()
        {
            using var context = new AuditDbContext(_options);
            await SeedDraftSalesReportAsync(context);
            var controller = CreateController(context);
            var images = new List<IFormFile>
            {
                CreateMockFormFile("1.jpg", "abc"),
                CreateMockFormFile("2.jpg", "abc"),
                CreateMockFormFile("3.jpg", "abc"),
                CreateMockFormFile("4.jpg", "abc"),
                CreateMockFormFile("5.jpg", "abc"),
                CreateMockFormFile("6.jpg", "abc")
            };
            
            var result = await controller.Upload(1, DateTime.Today, DateTime.Today, "Cashier", images);
            
            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.Contains(controller.ModelState[string.Empty]!.Errors, e => e.ErrorMessage.Contains("up to 5", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Upload_PostWithValidMultipleImages_SavesDraftReportAndImages()
        {
            using var context = new AuditDbContext(_options);
            await SeedDraftSalesReportAsync(context);
            var controller = CreateController(context);
            var images = new List<IFormFile>
            {
                CreateMockFormFile("1.jpg", "abc"),
                CreateMockFormFile("2.jpg", "def")
            };
            
            var result = await controller.Upload(1, new DateTime(2026, 8, 10), new DateTime(2026, 8, 11), "Cashier Main", images);
            
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(SalesReportsController.OpeningReview), redirect.ActionName);
            
            // Retrieve from database to verify
            var report = await context.SalesReports.Include(r => r.DocumentRecord).OrderByDescending(r => r.Id).FirstAsync();
            Assert.Equal(1, report.EstablishmentId);
            Assert.Equal(SalesReportStatus.Draft, report.Status);
            Assert.Equal("Cashier Main", report.CashierName);
            Assert.NotNull(report.ImageUrls);
            Assert.Equal(2, report.ImageUrls.Count);
            Assert.Contains("/SalesReports/Image/", report.ImageUrls[0]);
            Assert.Contains("/SalesReports/Image/", report.ImageUrls[1]);
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
        public async Task Create_RejectsDefaultCoverageDatesWithoutSaving()
        {
            using var context = new AuditDbContext(_options);
            await SeedManagersAsync(context);

            var missingStartController = CreateController(context);
            var missingStartResult = await missingStartController.Create(NewCoverageForm(2, 3, DateTime.MinValue, new DateTime(2026, 8, 12)));

            Assert.IsType<ViewResult>(missingStartResult);
            Assert.False(missingStartController.ModelState.IsValid);
            Assert.Contains(missingStartController.ModelState[nameof(CoverageCreateForm.StartDate)]!.Errors, error => error.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));

            var missingEndController = CreateController(context);
            var missingEndResult = await missingEndController.Create(NewCoverageForm(2, 3, new DateTime(2026, 8, 11), DateTime.MinValue));

            Assert.IsType<ViewResult>(missingEndResult);
            Assert.False(missingEndController.ModelState.IsValid);
            Assert.Contains(missingEndController.ModelState[nameof(CoverageCreateForm.EndDate)]!.Errors, error => error.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(await context.ManagerCoverages.ToListAsync());
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

    public class ReportsAuditPacketTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public ReportsAuditPacketTests()
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

        private static ReportsController CreateReportsController(AuditDbContext context, int currentUserId = 1, string currentUserRole = "Owner")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()),
                new Claim(ClaimTypes.Role, currentUserRole)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

            return new ReportsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = principal }
                },
                TempData = new TempDataDictionary(new DefaultHttpContext { User = principal }, new FakeTempDataProvider())
            };
        }

        private static async Task SeedReportBaseAsync(AuditDbContext context)
        {
            context.Users.AddRange(
                new User { Id = 1, Name = "Alice Owner", Email = "alice@test.com", PasswordHash = "hash", Role = UserRole.Owner, PcfBalance = 1000m, DailyStartingFloat = 1000m },
                new User { Id = 2, Name = "Maya Treasury", Email = "maya@test.com", PasswordHash = "hash", Role = UserRole.Manager, IsTreasury = true, PcfBalance = 500m, DailyStartingFloat = 500m },
                new User { Id = 3, Name = "Beth Buyer", Email = "beth@test.com", PasswordHash = "hash", Role = UserRole.Buyer, ManagerId = 2, PcfBalance = 200m, DailyStartingFloat = 200m },
                new User { Id = 4, Name = "Dayo Buyer", Email = "dayo@test.com", PasswordHash = "hash", Role = UserRole.Buyer, ManagerId = 2, PcfBalance = 150m, DailyStartingFloat = 150m });
            context.Establishments.AddRange(
                new Establishment { Id = 1, Name = "CKR Main" },
                new Establishment { Id = 2, Name = "Dayo" });
        }

        [Fact]
        public async Task Index_DateRangePopulatesAllBuyerLiquidationReportsWithoutBuyerFilter()
        {
            using var context = new AuditDbContext(_options);
            await SeedReportBaseAsync(context);
            context.PcfReleases.AddRange(
                new PcfRelease { Id = 11, ReceiverUserId = 3, ReleasedByTreasuryUserId = 2, ReleaseDate = new DateTime(2026, 8, 6), Amount = 5000m, Status = PcfReleaseStatus.Released },
                new PcfRelease { Id = 12, ReceiverUserId = 4, ReleasedByTreasuryUserId = 2, ReleaseDate = new DateTime(2026, 8, 6), Amount = 3000m, Status = PcfReleaseStatus.Released });
            context.AuditItems.AddRange(
                new AuditItem
                {
                    Id = 21,
                    BuyerId = 3,
                    EstablishmentId = 1,
                    EntryDate = new DateTime(2026, 8, 6),
                    Amount = 1200m,
                    Description = "Beth receipt",
                    Status = AuditStatus.Approved,
                    Details = new List<AuditItemDetail> { new AuditItemDetail { ItemName = "Supplies", Quantity = 1, Price = 1200m, Total = 1200m, AssignedEstablishmentId = 1 } }
                },
                new AuditItem
                {
                    Id = 22,
                    BuyerId = 4,
                    EstablishmentId = 2,
                    EntryDate = new DateTime(2026, 8, 6),
                    Amount = 750m,
                    Description = "Dayo receipt",
                    Status = AuditStatus.Approved,
                    Details = new List<AuditItemDetail> { new AuditItemDetail { ItemName = "Groceries", Quantity = 1, Price = 750m, Total = 750m, AssignedEstablishmentId = 2 } }
                });
            context.AuditSettlements.Add(new AuditSettlement { PcfReleaseId = 11, ReceiverUserId = 3, ResponsibleManagerId = 2, ProcessedByUserId = 2, TotalPCReleased = 5000m, TotalAcceptedExpenses = 1200m, ActualChangeReturned = 3800m, Status = AuditSettlementStatus.Confirmed });
            await context.SaveChangesAsync();

            var controller = CreateReportsController(context);
            var result = await controller.Index(new ReportsFilterViewModel { StartDate = new DateTime(2026, 8, 6), EndDate = new DateTime(2026, 8, 6) });

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ReportsViewModel>(viewResult.Model);
            Assert.Equal(2, model.BuyerAudits.Count);
            var beth = Assert.Single(model.BuyerAudits, report => report.BuyerName == "Beth Buyer");
            Assert.Equal(5000m, beth.TotalPc);
            Assert.Equal(1200m, beth.TotalExpenses);
            Assert.Equal(3800m, beth.ActualChangeReturned);
            var dayo = Assert.Single(model.BuyerAudits, report => report.BuyerName == "Dayo Buyer");
            Assert.Equal(3000m, dayo.TotalPc);
            Assert.Equal(750m, dayo.TotalExpenses);
        }

        [Fact]
        public async Task Index_BuyerFilterNarrowsBuyerLiquidationReports()
        {
            using var context = new AuditDbContext(_options);
            await SeedReportBaseAsync(context);
            context.PcfReleases.AddRange(
                new PcfRelease { ReceiverUserId = 3, ReleasedByTreasuryUserId = 2, ReleaseDate = new DateTime(2026, 8, 6), Amount = 5000m, Status = PcfReleaseStatus.Released },
                new PcfRelease { ReceiverUserId = 4, ReleasedByTreasuryUserId = 2, ReleaseDate = new DateTime(2026, 8, 6), Amount = 3000m, Status = PcfReleaseStatus.Released });
            await context.SaveChangesAsync();

            var controller = CreateReportsController(context);
            var result = await controller.Index(new ReportsFilterViewModel { StartDate = new DateTime(2026, 8, 6), EndDate = new DateTime(2026, 8, 6), BuyerId = 3 });

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ReportsViewModel>(viewResult.Model);
            var report = Assert.Single(model.BuyerAudits);
            Assert.Equal("Beth Buyer", report.BuyerName);
            Assert.Equal(5000m, report.TotalPc);
        }

        [Fact]
        public async Task Index_BuyerRoleOnlyShowsOwnLiquidationReport()
        {
            using var context = new AuditDbContext(_options);
            await SeedReportBaseAsync(context);
            context.PcfReleases.AddRange(
                new PcfRelease { ReceiverUserId = 3, ReleasedByTreasuryUserId = 2, ReleaseDate = new DateTime(2026, 8, 6), Amount = 5000m, Status = PcfReleaseStatus.Released },
                new PcfRelease { ReceiverUserId = 4, ReleasedByTreasuryUserId = 2, ReleaseDate = new DateTime(2026, 8, 6), Amount = 3000m, Status = PcfReleaseStatus.Released });
            await context.SaveChangesAsync();

            var controller = CreateReportsController(context, currentUserId: 3, currentUserRole: "Buyer");
            var result = await controller.Index(new ReportsFilterViewModel { StartDate = new DateTime(2026, 8, 6), EndDate = new DateTime(2026, 8, 6) });

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ReportsViewModel>(viewResult.Model);
            var report = Assert.Single(model.BuyerAudits);
            Assert.Equal("Beth Buyer", report.BuyerName);
            Assert.Equal(5000m, report.TotalPc);
        }

        [Fact]
        public async Task Index_BranchAuditUsesAssignedBranchThenFallsBackToAuditBranch()
        {
            using var context = new AuditDbContext(_options);
            await SeedReportBaseAsync(context);
            context.AuditItems.AddRange(
                new AuditItem
                {
                    BuyerId = 3,
                    EstablishmentId = 1,
                    EntryDate = new DateTime(2026, 8, 6),
                    Amount = 100m,
                    Description = "Old CKR receipt",
                    Status = AuditStatus.Approved,
                    Details = new List<AuditItemDetail> { new AuditItemDetail { ItemName = "Old line", Quantity = 1, Price = 100m, Total = 100m } }
                },
                new AuditItem
                {
                    BuyerId = 3,
                    EstablishmentId = 1,
                    EntryDate = new DateTime(2026, 8, 6),
                    Amount = 200m,
                    Description = "Split receipt",
                    Status = AuditStatus.Approved,
                    Details = new List<AuditItemDetail> { new AuditItemDetail { ItemName = "Dayo line", Quantity = 1, Price = 200m, Total = 200m, AssignedEstablishmentId = 2 } }
                });
            await context.SaveChangesAsync();

            var ckrResult = await CreateReportsController(context).Index(new ReportsFilterViewModel { StartDate = new DateTime(2026, 8, 6), EndDate = new DateTime(2026, 8, 6), EstablishmentId = 1 });
            var ckrModel = Assert.IsType<ReportsViewModel>(Assert.IsType<ViewResult>(ckrResult).Model);
            Assert.Contains(ckrModel.BranchAudit.Expenses, expense => expense.Description == "Old line" && expense.Allocation == "CKR Main");
            Assert.DoesNotContain(ckrModel.BranchAudit.Expenses, expense => expense.Description == "Dayo line");

            var dayoResult = await CreateReportsController(context).Index(new ReportsFilterViewModel { StartDate = new DateTime(2026, 8, 6), EndDate = new DateTime(2026, 8, 6), EstablishmentId = 2 });
            var dayoModel = Assert.IsType<ReportsViewModel>(Assert.IsType<ViewResult>(dayoResult).Model);
            Assert.Contains(dayoModel.BranchAudit.Expenses, expense => expense.Description == "Dayo line" && expense.Allocation == "Dayo");
            Assert.DoesNotContain(dayoModel.BranchAudit.Expenses, expense => expense.Description == "Old line");
        }

        [Fact]
        public async Task Index_TreasuryAuditIncludesVisibleCashOutDetails()
        {
            using var context = new AuditDbContext(_options);
            await SeedReportBaseAsync(context);
            context.TreasuryCashFlows.Add(new TreasuryCashFlow
            {
                Id = 31,
                TreasuryUserId = 2,
                CashFlowDate = new DateTime(2026, 8, 6),
                StartingBalance = 1000m,
                Entries = new List<CashFlowEntry>
                {
                    new CashFlowEntry { Direction = CashFlowDirection.In, Category = CashFlowCategory.Sales, EstablishmentId = 1, Amount = 1500m, CreatedByUserId = 2 },
                    new CashFlowEntry { Direction = CashFlowDirection.Out, Category = CashFlowCategory.PcfRelease, RelatedUserId = 3, Amount = 500m, Notes = "PCF Beth", CreatedByUserId = 2 }
                }
            });
            await context.SaveChangesAsync();

            var result = await CreateReportsController(context).Index(new ReportsFilterViewModel { StartDate = new DateTime(2026, 8, 6), EndDate = new DateTime(2026, 8, 6), TreasuryHandlerId = 2 });

            var model = Assert.IsType<ReportsViewModel>(Assert.IsType<ViewResult>(result).Model);
            var cashOut = Assert.Single(model.TreasuryAudit.CashOutRows);
            Assert.Equal("Beth Buyer", cashOut.Description);
            Assert.Equal("PcfRelease", cashOut.Category);
            Assert.Equal("Maya Treasury", cashOut.TreasuryHandlerName);
            Assert.Equal(500m, cashOut.Amount);
        }

        [Fact]
        public void ReportsView_RendersAllAuditPacketSectionsWithScrollableTables()
        {
            var viewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AuditCkDayo", "Views", "Reports", "Index.cshtml"));
            var view = File.ReadAllText(viewPath);

            Assert.Contains("Manager Audit / Receipt Audit Log", view);
            Assert.Contains("Buyer Liquidation", view);
            Assert.Contains("Branch Audit / Expense Allocations", view);
            Assert.Contains("Cash Out Details", view);
            Assert.Contains("overflow-y: auto; overflow-x: auto;", view);
            Assert.DoesNotContain("Latest 25", view);
        }

    }

    public class PnlRegistrationControllerTests
    {
        [Fact]
        public async Task Create_AddsRegisteredCategoryForAdmin()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var controller = CreateController(context);

            var result = await controller.Create("Beers", PnlExpenseSection.COGS);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(PnlRegistrationController.Index), redirect.ActionName);
            var category = Assert.Single(await context.PnlCategories.ToListAsync());
            Assert.Equal("Beers", category.Name);
            Assert.Equal(PnlExpenseSection.COGS, category.Section);
            Assert.True(category.IsActive);
        }

        private static PnlRegistrationController CreateController(AuditDbContext context)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Role, "Admin")
            };
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            };

            return new PnlRegistrationController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = new TempDataDictionary(httpContext, new FakeTempDataProvider())
            };
        }
    }

    public class BranchStaffNavigationPolicyTests
    {
        [Theory]
        [InlineData(nameof(AuditsController.Upload))]
        [InlineData(nameof(AuditsController.ProcessUpload))]
        public void ReceiptAuditEntryPoints_AllowBranchStaffForPcfPurchases(string actionName)
        {
            var method = typeof(AuditsController).GetMethods()
                .Single(method => method.Name == actionName);

            var authorize = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false).Cast<AuthorizeAttribute>());

            Assert.Contains("BranchStaff", authorize.Roles ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SalesReportUpload_AllowsBranchStaff()
        {
            var authorize = Assert.Single(typeof(SalesReportsController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
                .Cast<AuthorizeAttribute>());

            Assert.Contains("BranchStaff", authorize.Roles ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ReportsController_AllowsOnlyOwnersAndManagers()
        {
            var authorize = Assert.Single(typeof(ReportsController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
                .Cast<AuthorizeAttribute>());

            Assert.Equal("Owner,Manager,Auditor", authorize.Roles);
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

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; set; }
        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            CallCount++;
            Requests.Add(request);

            var url = request.RequestUri?.ToString() ?? "";
            string content = "{}";

            if (url.Contains("transcriptions"))
            {
                content = "{\"text\": \"What is our net profit for this month?\"}";
            }
            else if (url.Contains("completions"))
            {
                content = "{\"choices\": [{\"message\": {\"content\": \"The net profit is 1,234.56 pesos.\"}}]}";
            }
            else if (url.Contains("generateContent"))
            {
                content = "{\"candidates\": [{\"content\": {\"parts\": [{\"text\": \"The net profit is 1,234.56 pesos.\"}]}}]}";
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    public class VoiceControllerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public VoiceControllerTests()
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

        [Fact]
        public async Task VoiceController_ProcessesOwnerVoiceQuerySuccessfully()
        {
            using (var context = new AuditDbContext(_options))
            {
                var establishment = new Establishment { Id = 1, Name = "Main Branch", IsOperatingBranch = true, IsActive = true };
                context.Establishments.Add(establishment);

                var buyer = new User { Id = 1, Name = "Buyer One", Email = "buyer@test.com", PasswordHash = "hash", Role = UserRole.Buyer };
                context.Users.Add(buyer);

                var auditItem = new AuditItem
                {
                    Id = 1,
                    EstablishmentId = 1,
                    BuyerId = 1,
                    Status = AuditStatus.Approved,
                    Amount = 100m,
                    Description = "Sample expense",
                    SubmittedAt = DateTime.UtcNow
                };
                context.AuditItems.Add(auditItem);

                var doc = new DocumentRecord { Id = 1, ImageUrl = "/test.xlsx", UploadedAt = DateTime.UtcNow, UploadedByUserId = 1, OcrStatus = OcrStatus.Parsed, ReviewStatus = DocumentReviewStatus.Confirmed };
                context.DocumentRecords.Add(doc);

                var salesReport = new SalesReport
                {
                    Id = 1,
                    EstablishmentId = 1,
                    DocumentRecordId = 1,
                    Status = SalesReportStatus.Confirmed,
                    BusinessDate = DateTime.Today,
                    HandoverDate = DateTime.Today,
                    GrossSales = 500m,
                    CashOut = 200m,
                    ConfirmedCashToHandover = 300m
                };
                context.SalesReports.Add(salesReport);

                await context.SaveChangesAsync();
            }

            using (var context = new AuditDbContext(_options))
            {
                var voiceBiService = new VoiceBiService(context);
                var config = new MockConfiguration("gsk_dummy_key_for_testing_purposes_only");
                var handler = new MockHttpMessageHandler();
                var httpClient = new HttpClient(handler);

                var controller = new VoiceController(voiceBiService, config, httpClient);

                var audioBytes = new byte[] { 0x0, 0x1, 0x2, 0x3 };
                var ms = new MemoryStream(audioBytes);
                var audioFile = new FormFile(ms, 0, audioBytes.Length, "audioFile", "query.wav")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "audio/wav"
                };

                var result = await controller.UploadQuery(audioFile);

                var okResult = Assert.IsType<OkObjectResult>(result);
                var data = okResult.Value;
                Assert.NotNull(data);
                
                var json = JsonSerializer.Serialize(data);
                Assert.Contains("The net profit is 1,234.56 pesos.", json);
                Assert.True(handler.CallCount >= 2);
            }
        }

        [Fact]
        public async Task VoiceBiService_IncludesDetailedDailySalesReportsAndLines()
        {
            using (var context = new AuditDbContext(_options))
            {
                var establishment = new Establishment { Id = 10, Name = "CKR Main", IsOperatingBranch = true, IsActive = true };
                context.Establishments.Add(establishment);

                var uploader = new User { Id = 10, Name = "Staff", Email = "staff@test.com", PasswordHash = "hash", Role = UserRole.BranchStaff };
                context.Users.Add(uploader);

                var doc = new DocumentRecord { Id = 10, ImageUrl = "/test.xlsx", UploadedAt = DateTime.UtcNow, UploadedByUserId = 10, OcrStatus = OcrStatus.Parsed, ReviewStatus = DocumentReviewStatus.Confirmed };
                context.DocumentRecords.Add(doc);

                var salesReport = new SalesReport
                {
                    Id = 10,
                    EstablishmentId = 10,
                    DocumentRecordId = 10,
                    Status = SalesReportStatus.Confirmed,
                    BusinessDate = DateTime.Today,
                    HandoverDate = DateTime.Today,
                    GrossSales = 500m,
                    CashSales = 150m,
                    FoodSales = 300m,
                    BeerSales = 50m,
                    BeverageSales = 50m
                };
                salesReport.Lines.Add(new SalesReportLine { LineType = SalesReportLineType.GCash, Amount = 456m, Label = "GCash Line" });
                salesReport.Lines.Add(new SalesReportLine { LineType = SalesReportLineType.BankTransfer, Amount = 529m, Label = "BDO" });

                context.SalesReports.Add(salesReport);
                await context.SaveChangesAsync();
            }

            using (var context = new AuditDbContext(_options))
            {
                var service = new VoiceBiService(context);
                var today = DateTime.Today;
                var json = await service.GetPnlSummaryJsonAsync(today.AddDays(-1), today.AddDays(1));

                Assert.Contains("DailySalesReports", json);
                Assert.Contains("CashSales", json);
                Assert.Contains("GCash Line", json);
                Assert.Contains("BDO", json);
            }
        }
    }

    public class FakeAuthenticationService : Microsoft.AspNetCore.Authentication.IAuthenticationService
    {
        public System.Security.Claims.ClaimsPrincipal? Principal { get; private set; }
        public Microsoft.AspNetCore.Authentication.AuthenticationProperties? Properties { get; private set; }

        public Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> AuthenticateAsync(Microsoft.AspNetCore.Http.HttpContext context, string? scheme)
        {
            return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
        }

        public Task ChallengeAsync(Microsoft.AspNetCore.Http.HttpContext context, string? scheme, Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(Microsoft.AspNetCore.Http.HttpContext context, string? scheme, Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(Microsoft.AspNetCore.Http.HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties)
        {
            Principal = principal;
            Properties = properties;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(Microsoft.AspNetCore.Http.HttpContext context, string? scheme, Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }
    }

    public class FakeUrlHelperFactory : Microsoft.AspNetCore.Mvc.Routing.IUrlHelperFactory
    {
        public IUrlHelper GetUrlHelper(ActionContext context)
        {
            return new UsersControllerTests.FakeUrlHelper();
        }
    }

    public class FakeServiceProvider : IServiceProvider
    {
        private readonly Microsoft.AspNetCore.Authentication.IAuthenticationService _authService;

        public FakeServiceProvider(Microsoft.AspNetCore.Authentication.IAuthenticationService authService)
        {
            _authService = authService;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService))
            {
                return _authService;
            }
            if (serviceType == typeof(Microsoft.AspNetCore.Mvc.Routing.IUrlHelperFactory))
            {
                return new FakeUrlHelperFactory();
            }
            return null;
        }
    }

    public class AccountControllerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public AccountControllerTests()
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

        [Fact]
        public async Task Login_RedirectsOwnerToVoiceQuery()
        {
            using (var context = new AuditDbContext(_options))
            {
                var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
                context.Users.Add(new User { Id = 1, Name = "Alice Owner", Email = "owner@test.com", PasswordHash = passwordHash, Role = UserRole.Owner });
                await context.SaveChangesAsync();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = new AccountController(context);

                var authService = new FakeAuthenticationService();
                var httpContext = new DefaultHttpContext
                {
                    RequestServices = new FakeServiceProvider(authService)
                };
                controller.ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                };

                var model = new LoginViewModel
                {
                    Email = "owner@test.com",
                    Password = "Password123!"
                };

                var result = await controller.Login(model);

                var redirect = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Query", redirect.ActionName);
                Assert.Equal("Voice", redirect.ControllerName);
            }
        }

        [Fact]
        public async Task Login_RedirectsManagerToHomeIndex()
        {
            using (var context = new AuditDbContext(_options))
            {
                var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
                context.Users.Add(new User { Id = 2, Name = "Bob Manager", Email = "manager@test.com", PasswordHash = passwordHash, Role = UserRole.Manager });
                await context.SaveChangesAsync();
            }

            using (var context = new AuditDbContext(_options))
            {
                var controller = new AccountController(context);

                var authService = new FakeAuthenticationService();
                var httpContext = new DefaultHttpContext
                {
                    RequestServices = new FakeServiceProvider(authService)
                };
                controller.ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                };

                var model = new LoginViewModel
                {
                    Email = "manager@test.com",
                    Password = "Password123!"
                };

                var result = await controller.Login(model);

                var redirect = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal("Index", redirect.ActionName);
                Assert.Equal("Home", redirect.ControllerName);
            }
        }
    }

    public class ManagerCoverageUsabilityTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public ManagerCoverageUsabilityTests()
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

        [Fact]
        public async Task HomeController_Index_IncludesCoveredManagersTasksOnActiveCoverage()
        {
            using (var context = new AuditDbContext(_options))
            {
                // Seed Manager A (ID 10) & Buyer A (ID 11)
                var managerA = new User { Id = 10, Name = "Manager A", Email = "a@mgr.com", PasswordHash = "hash", Role = UserRole.Manager };
                var buyerA = new User { Id = 11, Name = "Buyer A", Email = "a@buyer.com", PasswordHash = "hash", Role = UserRole.Buyer, ManagerId = 10 };

                // Seed Manager B (ID 20) & Buyer B (ID 21)
                var managerB = new User { Id = 20, Name = "Manager B", Email = "b@mgr.com", PasswordHash = "hash", Role = UserRole.Manager };
                var buyerB = new User { Id = 21, Name = "Buyer B", Email = "b@buyer.com", PasswordHash = "hash", Role = UserRole.Buyer, ManagerId = 20 };

                var establishment = new Establishment { Id = 1, Name = "Main Branch", IsOperatingBranch = true, IsActive = true };

                context.Users.AddRange(managerA, buyerA, managerB, buyerB);
                context.Establishments.Add(establishment);
                await context.SaveChangesAsync();

                // Create a pending audit for Buyer A (reports to Manager A)
                var audit = new AuditItem
                {
                    Id = 1,
                    EstablishmentId = 1,
                    BuyerId = 11,
                    Status = AuditStatus.AwaitingManagerApproval,
                    Amount = 100m,
                    Description = "Sample audit"
                };
                context.AuditItems.Add(audit);

                // Setup Active Coverage: Manager B covers Manager A today
                context.ManagerCoverages.Add(new ManagerCoverage
                {
                    CoveredManagerId = 10,
                    CoveringManagerId = 20,
                    StartDate = DateTime.Today.AddDays(-1),
                    EndDate = DateTime.Today.AddDays(1),
                    Scope = CoverageScope.All,
                    IsActive = true,
                    CreatedByUserId = 10,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            using (var context = new AuditDbContext(_options))
            {
                // Mock HomeController for Manager B (ID 20)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, "20"),
                    new Claim(ClaimTypes.Role, "Manager")
                };
                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
                var controller = new HomeController(null, context, new CoverageService(context))
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = new DefaultHttpContext { User = principal }
                    }
                };

                var result = await controller.Index(new DashboardViewModel());
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<DashboardViewModel>(viewResult.Model);

                // Manager B should see Manager A's buyer's pending audit in their queue!
                var pendingAudit = Assert.Single(model.Audits);
                Assert.Equal(11, pendingAudit.BuyerId);
                Assert.Equal(100m, pendingAudit.Amount);
            }
        }

        [Fact]
        public async Task AuditsController_VerifyList_IncludesCoveredManagersAudits()
        {
            using (var context = new AuditDbContext(_options))
            {
                // Seed Manager A (ID 10) & Buyer A (ID 11)
                var managerA = new User { Id = 10, Name = "Manager A", Email = "a@mgr.com", PasswordHash = "hash", Role = UserRole.Manager };
                var buyerA = new User { Id = 11, Name = "Buyer A", Email = "a@buyer.com", PasswordHash = "hash", Role = UserRole.Buyer, ManagerId = 10 };

                // Seed Manager B (ID 20) & Buyer B (ID 21)
                var managerB = new User { Id = 20, Name = "Manager B", Email = "b@mgr.com", PasswordHash = "hash", Role = UserRole.Manager };
                var buyerB = new User { Id = 21, Name = "Buyer B", Email = "b@buyer.com", PasswordHash = "hash", Role = UserRole.Buyer, ManagerId = 20 };

                var establishment = new Establishment { Id = 1, Name = "Main Branch", IsOperatingBranch = true, IsActive = true };

                context.Users.AddRange(managerA, buyerA, managerB, buyerB);
                context.Establishments.Add(establishment);
                await context.SaveChangesAsync();

                // Create a pending audit for Buyer A (reports to Manager A)
                var audit = new AuditItem
                {
                    Id = 1,
                    EstablishmentId = 1,
                    BuyerId = 11,
                    Status = AuditStatus.AwaitingManagerApproval,
                    Amount = 100m,
                    Description = "Sample audit"
                };
                context.AuditItems.Add(audit);

                // Setup Active Coverage: Manager B covers Manager A today
                context.ManagerCoverages.Add(new ManagerCoverage
                {
                    CoveredManagerId = 10,
                    CoveringManagerId = 20,
                    StartDate = DateTime.Today.AddDays(-1),
                    EndDate = DateTime.Today.AddDays(1),
                    Scope = CoverageScope.All,
                    IsActive = true,
                    CreatedByUserId = 10,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            using (var context = new AuditDbContext(_options))
            {
                // Mock AuditsController for Manager B (ID 20)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, "20"),
                    new Claim(ClaimTypes.Role, "Manager")
                };
                var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
                var httpContext = new DefaultHttpContext { User = principal };
                var tempData = new TempDataDictionary(httpContext, new FakeTempDataProvider());

                var controller = new AuditsController(context, new UsersControllerTests.FakeOcrService(), new UsersControllerTests.FakeWebHostEnvironment(), new CoverageService(context))
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = httpContext
                    },
                    TempData = tempData,
                    Url = new UsersControllerTests.FakeUrlHelper()
                };

                // 1. VerifyList GET should include the covered audit
                var result = await controller.VerifyList();
                var viewResult = Assert.IsType<ViewResult>(result);
                var model = Assert.IsAssignableFrom<IEnumerable<AuditItem>>(viewResult.Model);
                var pendingAudit = Assert.Single(model);
                Assert.Equal(1, pendingAudit.Id);

                // 2. Verify POST should approve the audit successfully (not throw Forbid!)
                var postResult = await controller.Verify(1, AuditStatus.Approved);
                var redirectResult = Assert.IsType<RedirectToActionResult>(postResult);
                Assert.Equal(nameof(AuditsController.VerifyList), redirectResult.ActionName);

                var savedAudit = await context.AuditItems.FindAsync(1);
                Assert.Equal(AuditStatus.Approved, savedAudit.Status);
            }
        }
    }

    public class SalesReportsControllerTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public SalesReportsControllerTests()
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
            var establishment = new Establishment { Id = 1, Name = "Test Branch 1", IsOperatingBranch = true, IsActive = true };
            context.Establishments.Add(establishment);

            var owner = new User { Id = 1, Name = "Owner", Email = "owner@test.com", PasswordHash = "hash", Role = UserRole.Owner };
            context.Users.Add(owner);

            var document = new DocumentRecord
            {
                DocumentType = DocumentType.DailySalesReport,
                UploadedByUserId = 1,
                ImageUrl = "/sales/main.jpg",
                OcrStatus = OcrStatus.NotStarted,
                ReviewStatus = DocumentReviewStatus.Draft
            };
            context.DocumentRecords.Add(document);
            await context.SaveChangesAsync();

            var report = new SalesReport
            {
                Id = 1,
                EstablishmentId = 1,
                DocumentRecordId = document.Id,
                BusinessDate = new DateTime(2026, 8, 13),
                HandoverDate = new DateTime(2026, 8, 13),
                Status = SalesReportStatus.Draft
            };
            context.SalesReports.Add(report);
            await context.SaveChangesAsync();
        }

        private SalesReportsController CreateSalesReportsController(AuditDbContext context, int currentUserId, string currentUserRole)
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

            return new SalesReportsController(context, new UsersControllerTests.FakeOcrService())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = tempData
            };
        }

        [Fact]
        public async Task OpeningReview_ReturnsOpeningModel()
        {
            using (var context = new AuditDbContext(_options))
            {
                await SeedDataAsync(context);
                var controller = CreateSalesReportsController(context, 1, "Owner");
                var result = await controller.OpeningReview(1);
                var view = Assert.IsType<ViewResult>(result);
                var model = Assert.IsType<SalesReportReviewViewModel>(view.Model);
                Assert.Equal(SalesReportSection.Opening, model.ReportSection);
            }
        }

        [Fact]
        public void CombinedCashSales_IsSumOfOpeningAndClosing()
        {
            var model = new SalesReportReviewViewModel { OpeningCashSales = 300m, CashSales = 500m };
            Assert.Equal(800m, model.OpeningCashSales + model.CashSales);
        }
    }
}
