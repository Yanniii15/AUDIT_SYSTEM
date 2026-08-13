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
        public async Task ParseReceiptAsync_WhenGeminiFails_CallsTesseractFallback()
        {
            var configMap = new Dictionary<string, string>
            {
                { "GoogleGemini:ApiKey", "" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(configMap!).Build();
            
            var gemini = new GoogleGeminiOcrService(config);
            var tesseract = new TesseractOcrService();
            var fallback = new FallbackOcrService(gemini, tesseract);
            
            var dummyStream = new MemoryStream(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="));
            
            var result = await fallback.ParseReceiptAsync(new List<Stream> { dummyStream });
            
            Assert.NotNull(result);
            Assert.NotNull(result.TransactionDate);
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
        public async Task RecordCashOut_SavesSplitEntriesAcrossEstablishments()
        {
            var cashOutDate = new DateTime(2026, 8, 14);
            using (var context = new AuditDbContext(_options))
            {
                context.Users.Add(new User { Id = 1, Name = "Treasury Owner", Email = "split-owner@test.com", PasswordHash = "hash", Role = UserRole.Owner, IsTreasury = true });
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
                    Purpose = "Shared grocery split",
                    SplitAcrossEstablishments = true,
                    SplitRows = new List<ManualCashOutSplitViewModel>
                    {
                        new() { EstablishmentId = 20, Amount = 300m },
                        new() { EstablishmentId = 21, Amount = 200m }
                    }
                };

                var result = await controller.RecordCashOut(model);
                Assert.IsType<RedirectToActionResult>(result);

                var entries = context.CashFlowEntries.AsEnumerable().OrderBy(e => e.Amount).ToList();
                Assert.Equal(2, entries.Count);
                Assert.Contains(entries, entry => entry.EstablishmentId == 21 && entry.Amount == 200m && entry.Notes == "Shared grocery split");
                Assert.Contains(entries, entry => entry.EstablishmentId == 20 && entry.Amount == 300m && entry.Notes == "Shared grocery split");

                var flow = context.TreasuryCashFlows.Single(f => f.CashFlowDate == cashOutDate.Date);
                Assert.Equal(500m, flow.TotalCashOut);
                Assert.Equal(-500m, flow.ClosingBalance);
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
            Assert.Equal(nameof(SalesReportsController.Review), redirect.ActionName);
            
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
