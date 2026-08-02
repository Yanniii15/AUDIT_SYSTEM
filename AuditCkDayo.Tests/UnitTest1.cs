using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Controllers;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
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
                Assert.Equal("Index", redirectResult.ActionName);
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
                Assert.Equal("Index", redirectResult.ActionName);

                var buyer = await context.Users.FindAsync(3);
                Assert.Null(buyer.ManagerId);
            }
        }
    }
}
