using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AuditCkDayo.Controllers.Api;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.Services;

namespace AuditCkDayo.Tests
{
    public class ApiControllersTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AuditDbContext> _options;

        public ApiControllersTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var db = new AuditDbContext(_options);
            db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        [Fact]
        public void JwtTokenService_GenerateToken_ReturnsValidTokenWithClaims()
        {
            var key = "AuditCkDayo_SuperSecret_Jwt_Security_Key_For_Mobile_2026_CkrDayo_Secure";
            var service = new JwtTokenService(key, "AuditCkDayo");

            var user = new User
            {
                Id = 15,
                Name = "Maria Auditor",
                Email = "maria@test.com",
                Role = UserRole.Auditor,
                EstablishmentId = 2
            };

            var token = service.GenerateToken(user);
            Assert.False(string.IsNullOrWhiteSpace(token));

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            Assert.Equal("15", jwt.Subject);
            Assert.Equal("Auditor", jwt.Claims.First(c => c.Type == "role" || c.Type == ClaimTypes.Role).Value);
            Assert.Equal("2", jwt.Claims.First(c => c.Type == "EstablishmentId").Value);
        }

        [Fact]
        public async Task AuthApi_Login_SuccessReturnsTokenAndUserProfile()
        {
            using var db = new AuditDbContext(_options);
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("Secret123!");
            db.Users.Add(new User
            {
                Id = 20,
                Name = "Manager Mark",
                Email = "mark@test.com",
                PasswordHash = passwordHash,
                Role = UserRole.Manager
            });
            await db.SaveChangesAsync();

            var jwtService = new JwtTokenService("AuditCkDayo_SuperSecret_Jwt_Security_Key_For_Mobile_2026_CkrDayo_Secure");
            var controller = new AuthApiController(db, jwtService);

            var result = await controller.Login(new LoginRequest { Email = "mark@test.com", Password = "Secret123!" });
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task AuditsApi_GetAuditsByDate_ReturnsAuditsList()
        {
            using var db = new AuditDbContext(_options);
            var date = new DateTime(2026, 8, 27);
            db.Establishments.Add(new Establishment { Id = 1, Name = "Dayo" });
            db.Users.Add(new User { Id = 3, Name = "Buyer Bob", Email = "bob@test.com", PasswordHash = "h", Role = UserRole.Buyer });
            db.AuditItems.Add(new AuditItem
            {
                Id = 101,
                BuyerId = 3,
                EstablishmentId = 1,
                Amount = 1500m,
                Description = "Market Receipt",
                EntryDate = date,
                Status = AuditStatus.Approved,
                Details = new List<AuditItemDetail>
                {
                    new AuditItemDetail { ItemName = "Pork", Quantity = 2, Price = 750m, Total = 1500m, ExpenseSourceName = "MARKET" }
                }
            });
            await db.SaveChangesAsync();

            var controller = new AuditsApiController(db);
            var result = await controller.GetAuditsByDate(date, null, null);
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        [Fact]
        public async Task BranchApi_VerifyDelivery_MarksDetailVerified()
        {
            using var db = new AuditDbContext(_options);
            db.Establishments.Add(new Establishment { Id = 1, Name = "Dayo" });
            db.Users.Add(new User { Id = 3, Name = "Buyer", Email = "b@test.com", PasswordHash = "h", Role = UserRole.Buyer });
            var audit = new AuditItem
            {
                Id = 102,
                BuyerId = 3,
                EstablishmentId = 1,
                Amount = 500m,
                Description = "Goods",
                EntryDate = DateTime.Today,
                Status = AuditStatus.AwaitingBranchVerification,
                Details = new List<AuditItemDetail>
                {
                    new AuditItemDetail { Id = 201, ItemName = "Ice", Quantity = 1, Price = 500m, Total = 500m, BranchVerificationStatus = BranchVerificationStatus.Pending, AssignedEstablishmentId = 1 }
                }
            };
            db.AuditItems.Add(audit);
            await db.SaveChangesAsync();

            var controller = new BranchApiController(db);
            var result = await controller.VerifyDelivery(201);
            Assert.IsType<OkObjectResult>(result);

            var updatedDetail = await db.AuditItemDetails.FindAsync(201);
            Assert.Equal(BranchVerificationStatus.Verified, updatedDetail!.BranchVerificationStatus);
        }
    }
}
