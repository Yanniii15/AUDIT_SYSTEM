using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public class AuditSummaryTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public AuditSummaryTests()
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
            _connection.Close();
        }

        [Fact]
        public void ReportsController_IsRestrictedToOwnerManagerAdmin_NotAuditor()
        {
            var authAttribute = typeof(ReportsController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttribute);
            Assert.Contains("Owner", authAttribute.Roles);
            Assert.Contains("Manager", authAttribute.Roles);
            Assert.Contains("Admin", authAttribute.Roles);
            Assert.DoesNotContain("Auditor", authAttribute.Roles);
        }

        [Fact]
        public void AuditsController_SummaryAction_IsAuthorizedForOwnerAuditorAdmin_NotManager()
        {
            var method = typeof(AuditsController).GetMethod("Summary", new[] { typeof(AuditSummaryFilterViewModel) });
            Assert.NotNull(method);

            var authAttribute = method.GetCustomAttribute<AuthorizeAttribute>();
            Assert.NotNull(authAttribute);
            Assert.Contains("Owner", authAttribute.Roles);
            Assert.Contains("Auditor", authAttribute.Roles);
            Assert.Contains("Admin", authAttribute.Roles);
            Assert.DoesNotContain("Manager", authAttribute.Roles);
        }

        [Fact]
        public void AuditSummaryViewModel_CalculatesCorrectActualChangeAndShortOver()
        {
            var model = new AuditSummaryViewModel
            {
                BeginningBalance = 5000m,
                HandedChange = 3000m,
                PcfMatrix = new PcfMatrixViewModel
                {
                    TotalPc = 25000m,
                    TotalExpenses = 21000m
                }
            };

            Assert.Equal(25000m, model.TotalPc);
            Assert.Equal(21000m, model.TotalExpenses);
            Assert.Equal(4000m, model.ActualChange); // 25000 - 21000
            Assert.Equal(-1000m, model.ShortOver);    // 3000 - 4000 = -1000 (short)
        }

        [Fact]
        public async Task Summary_Action_ComputesBeginningBalanceAndReleasesCorrectly()
        {
            using var context = new AuditDbContext(_options);

            var establishment = new Establishment { Name = "Main Branch" };
            context.Establishments.Add(establishment);

            var buyer = new User
            {
                Name = "Buyer Bob",
                Email = "bob@test.com",
                PasswordHash = "hash",
                Role = UserRole.Buyer
            };
            var manager = new User
            {
                Name = "Manager Maymay",
                Email = "maymay@test.com",
                PasswordHash = "hash",
                Role = UserRole.Manager
            };
            var auditor = new User
            {
                Name = "Auditor Keith",
                Email = "keith@test.com",
                PasswordHash = "hash",
                Role = UserRole.Auditor
            };
            context.Users.AddRange(buyer, manager, auditor);
            await context.SaveChangesAsync();

            var startDate = new DateTime(2026, 8, 20);
            var endDate = new DateTime(2026, 8, 26);

            // Prior surrender for buyer before start date -> sets Beginning Balance
            var priorSurrender = new SurrenderRequest
            {
                BuyerId = buyer.Id,
                DeclaredAmount = 47998m,
                ConfirmedAmount = 47998m,
                Status = SurrenderStatus.Confirmed,
                RequestDate = startDate.AddDays(-1)
            };
            context.SurrenderRequests.Add(priorSurrender);

            // PCF Release during period
            var release = new PcfRelease
            {
                ReceiverUserId = buyer.Id,
                ReleasedByTreasuryUserId = manager.Id,
                Amount = 20000m,
                ReleaseDate = startDate.AddDays(1),
                Purpose = "Market"
            };
            context.PcfReleases.Add(release);

            // Approved Audit Item during period
            var auditItem = new AuditItem
            {
                BuyerId = buyer.Id,
                EstablishmentId = establishment.Id,
                Amount = 15000m,
                Status = AuditStatus.Approved,
                EntryDate = startDate.AddDays(2),
                Description = "Market purchases",
                Details = new List<AuditItemDetail>
                {
                    new AuditItemDetail
                    {
                        ItemName = "Fish and Pork",
                        Quantity = 1,
                        Price = 15000m,
                        Total = 15000m,
                        ReceiptStatus = ReceiptLineStatus.HasReceipt
                    }
                }
            };
            context.AuditItems.Add(auditItem);
            await context.SaveChangesAsync();

            var controller = new AuditsController(context, new UsersControllerTests.FakeOcrService(), new UsersControllerTests.FakeWebHostEnvironment(), new CoverageService(context))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, auditor.Id.ToString()),
                            new Claim(ClaimTypes.Role, "Auditor")
                        }, "TestAuth"))
                    }
                }
            };

            var filter = new AuditSummaryFilterViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                HandedChangeOverride = 53000m
            };

            var result = await controller.Summary(filter) as ViewResult;
            Assert.NotNull(result);

            var model = result.Model as AuditSummaryViewModel;
            Assert.NotNull(model);

            // Beginning balance should have been resolved from prior surrender: 47998
            Assert.Equal(47998m, model.BeginningBalance);

            // Total PC = Beginning (47998 under OTHERS) + Release (20000) = 67998
            Assert.Equal(67998m, model.TotalPc);

            // Total Expenses = 15000
            Assert.Equal(15000m, model.TotalExpenses);

            // Actual Change = 67998 - 15000 = 52998
            Assert.Equal(52998m, model.ActualChange);

            // Handed Change = 53000
            Assert.Equal(53000m, model.HandedChange);

            // Short / Over = 53000 - 52998 = +2
            Assert.Equal(2m, model.ShortOver);
        }

    }
}
