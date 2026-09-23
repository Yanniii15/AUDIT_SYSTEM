using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AuditCkDayo.Controllers;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace AuditCkDayo.Tests
{
    public class SalesReportDepositSlipTests
    {
        [Fact]
        public void SalesReport_HasDepositSlipProperties_AndComputesVarianceCorrectly()
        {
            var user = new User { Id = 1, Name = "Manager John" };
            var uploadTime = DateTime.UtcNow;
            var depositDate = DateTime.Today;

            var report = new SalesReport
            {
                ConfirmedCashToHandover = 50000.00m,
                DepositedAmount = 50000.00m,
                DepositSlipImageUrl = "/SalesReports/DepositSlip/test.jpg",
                DepositBankName = "BDO",
                DepositReferenceNumber = "TRN-12345",
                DepositDate = depositDate,
                DepositVarianceReason = "None",
                DepositUploadedByUserId = 1,
                DepositUploadedByUser = user,
                DepositUploadedAt = uploadTime
            };

            // Assert metadata properties
            Assert.Equal("/SalesReports/DepositSlip/test.jpg", report.DepositSlipImageUrl);
            Assert.Equal(50000.00m, report.DepositedAmount);
            Assert.Equal("BDO", report.DepositBankName);
            Assert.Equal("TRN-12345", report.DepositReferenceNumber);
            Assert.Equal(depositDate, report.DepositDate);
            Assert.Equal("None", report.DepositVarianceReason);
            Assert.Equal(1, report.DepositUploadedByUserId);
            Assert.Same(user, report.DepositUploadedByUser);
            Assert.Equal(uploadTime, report.DepositUploadedAt);

            // Assert matching deposit
            Assert.True(report.HasDepositSlip);
            Assert.Equal(0.00m, report.DepositVariance);
            Assert.True(report.IsDepositMatched);
            Assert.False(report.IsDepositDiscrepancy);

            // Test Short Variance (-500)
            report.DepositedAmount = 49500.00m;
            Assert.Equal(-500.00m, report.DepositVariance);
            Assert.False(report.IsDepositMatched);
            Assert.True(report.IsDepositDiscrepancy);

            // Test Over Variance (+200)
            report.DepositedAmount = 50200.00m;
            Assert.Equal(200.00m, report.DepositVariance);
            Assert.False(report.IsDepositMatched);
            Assert.True(report.IsDepositDiscrepancy);
        }

        [Fact]
        public void SalesReport_WithoutDepositSlip_HasDepositSlipIsFalse()
        {
            var report = new SalesReport
            {
                ConfirmedCashToHandover = 10000.00m,
                DepositSlipImageUrl = null,
                DepositedAmount = null
            };

            Assert.False(report.HasDepositSlip);
            Assert.False(report.IsDepositMatched);
            Assert.False(report.IsDepositDiscrepancy);
            Assert.Equal(-10000.00m, report.DepositVariance);

            report.DepositSlipImageUrl = "   ";
            Assert.False(report.HasDepositSlip);
        }

        [Fact]
        public void CashFlowEntry_HasSalesReportRelationshipProperties()
        {
            var property = typeof(CashFlowEntry).GetProperty("SalesReportId");
            Assert.NotNull(property);
            Assert.Equal(typeof(int?), property.PropertyType);

            var navProperty = typeof(CashFlowEntry).GetProperty("SalesReport");
            Assert.NotNull(navProperty);
            Assert.Equal(typeof(SalesReport), navProperty.PropertyType);
        }

        [Fact]
        public void AuditDbContext_CanPersistSalesReportDepositSlipFields()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var depositDate = new DateTime(2026, 9, 23, 10, 30, 0, DateTimeKind.Utc);
            var uploadTime = DateTime.UtcNow;

            int salesReportId;
            int cashFlowEntryId;

            using (var context = new AuditDbContext(options))
            {
                var report = new SalesReport
                {
                    ConfirmedCashToHandover = 12500.50m,
                    DepositedAmount = 12500.50m,
                    DepositBankName = "BPI",
                    DepositReferenceNumber = "REF-9988",
                    DepositDate = depositDate,
                    DepositVarianceReason = "Matched",
                    DepositSlipImageUrl = "/SalesReports/DepositSlip/test_receipt.jpg",
                    DepositUploadedAt = uploadTime
                };

                context.SalesReports.Add(report);
                context.SaveChanges();
                salesReportId = report.Id;

                var cashFlowEntry = new CashFlowEntry
                {
                    SalesReportId = report.Id,
                    Amount = 12500.50m,
                    Direction = CashFlowDirection.In,
                    Category = CashFlowCategory.Sales
                };

                context.CashFlowEntries.Add(cashFlowEntry);
                context.SaveChanges();
                cashFlowEntryId = cashFlowEntry.Id;
            }

            using (var context = new AuditDbContext(options))
            {
                var reloadedReport = context.SalesReports.FirstOrDefault(r => r.Id == salesReportId);
                Assert.NotNull(reloadedReport);
                Assert.Equal(12500.50m, reloadedReport.DepositedAmount);
                Assert.Equal("BPI", reloadedReport.DepositBankName);
                Assert.Equal("REF-9988", reloadedReport.DepositReferenceNumber);
                Assert.Equal(depositDate, reloadedReport.DepositDate);
                Assert.Equal("Matched", reloadedReport.DepositVarianceReason);
                Assert.Equal("/SalesReports/DepositSlip/test_receipt.jpg", reloadedReport.DepositSlipImageUrl);
                Assert.Equal(uploadTime, reloadedReport.DepositUploadedAt);
                Assert.True(reloadedReport.HasDepositSlip);
                Assert.True(reloadedReport.IsDepositMatched);
                Assert.Equal(0.00m, reloadedReport.DepositVariance);

                var reloadedEntry = context.CashFlowEntries
                    .Include(e => e.SalesReport)
                    .FirstOrDefault(e => e.Id == cashFlowEntryId);
                Assert.NotNull(reloadedEntry);
                Assert.Equal(salesReportId, reloadedEntry.SalesReportId);
                Assert.NotNull(reloadedEntry.SalesReport);
                Assert.Equal(salesReportId, reloadedEntry.SalesReport.Id);
                Assert.Equal("BPI", reloadedEntry.SalesReport.DepositBankName);
                Assert.Equal(12500.50m, reloadedEntry.SalesReport.DepositedAmount);
            }
        }

        [Theory]
        [InlineData("BDO UNIBANK CASH DEPOSIT TRN: 948201 AMOUNT: PHP 45,780.00 DATE: 2026-08-15", "BDO", 45780.00, "948201")]
        [InlineData("BANK OF THE PHILIPPINE ISLANDS REF# 88219 TOTAL CASH: 12,500.50 08/16/2026", "BPI", 12500.50, "88219")]
        [InlineData("METROBANK CASH DEPOSIT P35,000.00 TRACE 77123", "Metrobank", 35000.00, "77123")]
        public void DepositSlipOcrService_ExtractsFieldsFromRawTextCorrectly(string rawText, string expectedBank, decimal expectedAmount, string expectedRef)
        {
            var result = AuditCkDayo.Services.TesseractOcrService.ParseDepositSlipText(rawText);

            Assert.Equal(expectedBank, result.DetectedBank);
            Assert.Equal(expectedAmount, result.DetectedAmount);
            Assert.Contains(expectedRef, result.DetectedReference);
        }

        [Fact]
        public async Task UploadDepositSlip_RequiresVarianceReason_WhenAmountsDoNotMatch()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var report = new SalesReport
            {
                Id = 10,
                EstablishmentId = 1,
                BusinessDate = DateTime.Today,
                ConfirmedCashToHandover = 50000.00m,
                Status = SalesReportStatus.Confirmed
            };
            context.Users.Add(new User
            {
                Id = 2,
                Name = "Staff",
                Email = "staff10@test.com",
                PasswordHash = "hash",
                Role = UserRole.BranchStaff,
                EstablishmentId = 1,
                ManagerId = 1
            });
            context.SalesReports.Add(report);
            context.SaveChanges();

            var controller = new SalesReportsController(context, null!, null!);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "1"),
                new(ClaimTypes.Role, "Manager")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var result = await controller.UploadDepositSlip(new UploadDepositSlipRequest
            {
                SalesReportId = 10,
                DepositedAmount = 48000.00m,
                DepositBankName = "BDO",
                DepositReferenceNumber = "123",
                DepositVarianceReason = ""
            }, null);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Deposit Variance explanation is required", badRequest.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task UploadDepositSlip_SavesSuccessfully_WhenAmountsMatch()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var report = new SalesReport
            {
                Id = 20,
                EstablishmentId = 1,
                BusinessDate = DateTime.Today,
                ConfirmedCashToHandover = 50000.00m,
                Status = SalesReportStatus.Confirmed
            };
            context.Users.Add(new User
            {
                Id = 2,
                Name = "Staff",
                Email = "staff20@test.com",
                PasswordHash = "hash",
                Role = UserRole.BranchStaff,
                EstablishmentId = 1,
                ManagerId = 1
            });
            context.SalesReports.Add(report);
            context.SaveChanges();

            var controller = new SalesReportsController(context, null!, null!);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "1"),
                new(ClaimTypes.Role, "Manager")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var depositDate = new DateTime(2026, 9, 23);
            var result = await controller.UploadDepositSlip(new UploadDepositSlipRequest
            {
                SalesReportId = 20,
                DepositedAmount = 50000.00m,
                DepositBankName = "BPI",
                DepositReferenceNumber = "REF-9999",
                DepositDate = depositDate,
                DepositVarianceReason = null
            }, null);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            var updatedReport = await context.SalesReports.FindAsync(20);
            Assert.NotNull(updatedReport);
            Assert.Equal(50000.00m, updatedReport.DepositedAmount);
            Assert.Equal("BPI", updatedReport.DepositBankName);
            Assert.Equal("REF-9999", updatedReport.DepositReferenceNumber);
            Assert.Equal(depositDate, updatedReport.DepositDate);
            Assert.Equal(1, updatedReport.DepositUploadedByUserId);
            Assert.NotNull(updatedReport.DepositUploadedAt);
        }

        [Theory]
        [InlineData("../secret.jpg")]
        [InlineData("..\\secret.jpg")]
        [InlineData("../../etc/passwd")]
        [InlineData("sub/folder/test.png")]
        public void GetDepositSlipImage_RejectsPathTraversal(string maliciousFilename)
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var controller = new SalesReportsController(context, null!, null!);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "1"),
                new(ClaimTypes.Role, "Manager")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var result = controller.GetDepositSlipImage(maliciousFilename);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Invalid file name.", badRequest.Value);
        }

        [Fact]
        public async Task UploadDepositSlip_SavesFile_WhenFileProvided()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var report = new SalesReport
            {
                Id = 30,
                BusinessDate = DateTime.Today,
                ConfirmedCashToHandover = 1000m,
                Status = SalesReportStatus.Confirmed
            };
            context.SalesReports.Add(report);
            context.SaveChanges();

            var controller = new SalesReportsController(context, null!, null!);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Role, "Owner")
                    }, "TestAuth"))
                }
            };

            var content = "test content"u8.ToArray();
            var formFile = new FormFile(new System.IO.MemoryStream(content), 0, content.Length, "depositSlipFile", "sample.jpg");

            var result = await controller.UploadDepositSlip(new UploadDepositSlipRequest
            {
                SalesReportId = 30,
                DepositedAmount = 1000m,
                DepositBankName = "Metrobank",
                DepositReferenceNumber = "REF-111"
            }, formFile);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var updatedReport = await context.SalesReports.FindAsync(30);
            Assert.NotNull(updatedReport);
            Assert.NotNull(updatedReport.DepositSlipImageUrl);
            Assert.StartsWith("/SalesReports/DepositSlip/slip_30_", updatedReport.DepositSlipImageUrl);

            // Clean up created file if exists
            var fileName = System.IO.Path.GetFileName(updatedReport.DepositSlipImageUrl);
            var filePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "storage", "deposit_slips", fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        [Fact]
        public async Task UploadDepositSlip_ReturnsForbid_WhenUserCannotAccessEstablishment()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var report = new SalesReport
            {
                Id = 40,
                EstablishmentId = 99,
                BusinessDate = DateTime.Today,
                ConfirmedCashToHandover = 1000m,
                Status = SalesReportStatus.Confirmed
            };
            // Manager 1 only has access to establishment 1, not 99
            context.Users.Add(new User
            {
                Id = 2,
                Name = "Staff Branch 1",
                Email = "staff-branch1@test.com",
                PasswordHash = "hash",
                Role = UserRole.BranchStaff,
                EstablishmentId = 1,
                ManagerId = 1
            });
            context.SalesReports.Add(report);
            context.SaveChanges();

            var controller = new SalesReportsController(context, null!, null!);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "1"),
                new(ClaimTypes.Role, "Manager")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var result = await controller.UploadDepositSlip(new UploadDepositSlipRequest
            {
                SalesReportId = 40,
                DepositedAmount = 1000m,
                DepositBankName = "BDO",
                DepositReferenceNumber = "123"
            }, null);

            Assert.IsType<ForbidResult>(result);
        }

        [Theory]
        [InlineData("malicious.exe")]
        [InlineData("document.pdf")]
        [InlineData("script.sh")]
        [InlineData("image.bmp")]
        [InlineData("test.svg")]
        [InlineData("noextension")]
        public async Task UploadDepositSlip_ReturnsBadRequest_WhenFileExtensionIsInvalid(string invalidFileName)
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var report = new SalesReport
            {
                Id = 50,
                EstablishmentId = 1,
                BusinessDate = DateTime.Today,
                ConfirmedCashToHandover = 1000m,
                Status = SalesReportStatus.Confirmed
            };
            context.Users.Add(new User
            {
                Id = 2,
                Name = "Staff",
                Email = "staff50@test.com",
                PasswordHash = "hash",
                Role = UserRole.BranchStaff,
                EstablishmentId = 1,
                ManagerId = 1
            });
            context.SalesReports.Add(report);
            context.SaveChanges();

            var controller = new SalesReportsController(context, null!, null!);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "1"),
                new(ClaimTypes.Role, "Manager")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var content = "dummy file content"u8.ToArray();
            var formFile = new FormFile(new System.IO.MemoryStream(content), 0, content.Length, "depositSlipFile", invalidFileName);

            var result = await controller.UploadDepositSlip(new UploadDepositSlipRequest
            {
                SalesReportId = 50,
                DepositedAmount = 1000m,
                DepositBankName = "BDO",
                DepositReferenceNumber = "123"
            }, formFile);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Invalid file format", badRequest.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task UploadDepositSlip_ReturnsBadRequest_WhenDepositedAmountIsNegative()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var report = new SalesReport
            {
                Id = 60,
                EstablishmentId = 1,
                BusinessDate = DateTime.Today,
                ConfirmedCashToHandover = 1000m,
                Status = SalesReportStatus.Confirmed
            };
            context.Users.Add(new User
            {
                Id = 2,
                Name = "Staff",
                Email = "staff60@test.com",
                PasswordHash = "hash",
                Role = UserRole.BranchStaff,
                EstablishmentId = 1,
                ManagerId = 1
            });
            context.SalesReports.Add(report);
            context.SaveChanges();

            var controller = new SalesReportsController(context, null!, null!);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "1"),
                new(ClaimTypes.Role, "Manager")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var result = await controller.UploadDepositSlip(new UploadDepositSlipRequest
            {
                SalesReportId = 60,
                DepositedAmount = -500m,
                DepositBankName = "BDO",
                DepositReferenceNumber = "123"
            }, null);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("negative", badRequest.Value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExtractDepositSlipOcr_ReturnsBadRequest_WhenNoImageProvided()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var controller = new SalesReportsController(context, null!, null!);

            var result = await controller.ExtractDepositSlipOcr(null, new FakeDepositSlipOcrService());
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task ExtractDepositSlipOcr_ReturnsJson_WhenImageProvided()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var controller = new SalesReportsController(context, null!, null!);

            var content = "fake image"u8.ToArray();
            var formFile = new FormFile(new System.IO.MemoryStream(content), 0, content.Length, "depositSlipImage", "slip.jpg");
            var fakeOcr = new FakeDepositSlipOcrService
            {
                Result = new AuditCkDayo.Services.DepositSlipOcrResult
                {
                    Success = true,
                    DetectedBank = "BDO",
                    DetectedAmount = 15000m,
                    DetectedReference = "TRN-999",
                    DetectedDate = new DateTime(2026, 9, 23)
                }
            };

            var result = await controller.ExtractDepositSlipOcr(formFile, fakeOcr);
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.NotNull(jsonResult.Value);
        }

        [Fact]
        public void GetDepositSlipImage_ReturnsNotFound_WhenFileDoesNotExist()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var controller = new SalesReportsController(context, null!, null!);

            var result = controller.GetDepositSlipImage("nonexistent_safe_file.jpg");
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public void TreasuryCashFlowEntry_IdentifiesMissingDepositSlipState()
        {
            var report = new SalesReport
            {
                Id = 1,
                GrossSales = 50000.00m,
                ConfirmedCashToHandover = 50000.00m,
                DepositSlipImageUrl = null
            };

            var entry = new CashFlowEntry
            {
                Id = 10,
                Direction = CashFlowDirection.In,
                Category = CashFlowCategory.Sales,
                Amount = 50000.00m,
                SalesReportId = report.Id,
                SalesReport = report
            };

            // CashFlowEntry with Category == Sales and SalesReport with HasDepositSlip == false is flagged as missing slip
            Assert.Equal(CashFlowCategory.Sales, entry.Category);
            Assert.NotNull(entry.SalesReport);
            Assert.False(entry.SalesReport.HasDepositSlip);

            bool isMissingSlip = entry.Direction == CashFlowDirection.In
                                 && entry.Category == CashFlowCategory.Sales
                                 && (entry.SalesReport == null || !entry.SalesReport.HasDepositSlip);
            Assert.True(isMissingSlip);

            // Once DepositSlipImageUrl is set, HasDepositSlip is true
            report.DepositSlipImageUrl = "/SalesReports/DepositSlip/slip_123.jpg";
            report.DepositBankName = "BDO";
            Assert.True(entry.SalesReport!.HasDepositSlip);

            isMissingSlip = entry.Direction == CashFlowDirection.In
                            && entry.Category == CashFlowCategory.Sales
                            && (entry.SalesReport == null || !entry.SalesReport.HasDepositSlip);
            Assert.False(isMissingSlip);
        }

        [Fact]
        public async Task PostConfirmedSalesReportToTreasury_SetsSalesReportIdOnEntry()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var report = new SalesReport
            {
                Id = 42,
                EstablishmentId = 1,
                HandoverDate = DateTime.Today,
                BusinessDate = DateTime.Today,
                ConfirmedCashToHandover = 15000.00m,
                DocumentRecordId = 88,
                Status = SalesReportStatus.Confirmed
            };
            context.SalesReports.Add(report);
            await context.SaveChangesAsync();

            var controller = new SalesReportsController(context, null!, null!);
            int currentUserId = 5;

            // When PostConfirmedSalesReportToTreasury runs
            await controller.PostConfirmedSalesReportToTreasury(report, currentUserId);
            await context.SaveChangesAsync();

            // The created/updated CashFlowEntry has entry.SalesReportId == report.Id
            var entry = await context.CashFlowEntries
                .FirstOrDefaultAsync(e => e.Category == CashFlowCategory.Sales && e.SalesReportId == report.Id);

            Assert.NotNull(entry);
            Assert.Equal(report.Id, entry.SalesReportId);
            Assert.Equal(report.ConfirmedCashToHandover, entry.Amount);
            Assert.Equal(CashFlowCategory.Sales, entry.Category);
            Assert.Equal(CashFlowDirection.In, entry.Direction);

            // Updating existing entry
            report.ConfirmedCashToHandover = 18000.00m;
            await controller.PostConfirmedSalesReportToTreasury(report, currentUserId);
            await context.SaveChangesAsync();

            var updatedEntry = await context.CashFlowEntries
                .FirstOrDefaultAsync(e => e.Category == CashFlowCategory.Sales && e.SalesReportId == report.Id);
            Assert.NotNull(updatedEntry);
            Assert.Equal(report.Id, updatedEntry.SalesReportId);
            Assert.Equal(18000.00m, updatedEntry.Amount);
        }

        [Fact]
        public async Task SalesReportReviewViewModel_CorrectlyMapsAndFormatsDepositSlipDataForViews()
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new AuditDbContext(options);
            var uploader = new User
            {
                Id = 15,
                Name = "Reviewer Sarah",
                Role = UserRole.Owner,
                Email = "sarah@audit.com",
                PasswordHash = "hash"
            };
            context.Users.Add(uploader);

            var establishment = new Establishment { Id = 1, Name = "Branch 1" };
            context.Establishments.Add(establishment);

            var document = new DocumentRecord
            {
                Id = 105,
                DocumentType = DocumentType.DailySalesReport,
                UploadedByUserId = 15,
                ImageUrl = "/sales/main.jpg",
                OcrStatus = OcrStatus.Parsed,
                ReviewStatus = DocumentReviewStatus.Confirmed
            };
            context.DocumentRecords.Add(document);

            var depositDate = new DateTime(2026, 8, 25);
            var uploadedAt = new DateTime(2026, 8, 25, 16, 45, 0, DateTimeKind.Utc);
            var report = new SalesReport
            {
                Id = 105,
                DocumentRecordId = 105,
                EstablishmentId = 1,
                BusinessDate = depositDate,
                HandoverDate = depositDate,
                ConfirmedCashToHandover = 65000.00m,
                CashSales = 40000.00m,
                OpeningCashSales = 25000.00m,
                Status = SalesReportStatus.Confirmed,
                DepositSlipImageUrl = "/SalesReports/DepositSlip/slip_105.jpg",
                DepositedAmount = 65000.00m,
                DepositBankName = "BDO",
                DepositReferenceNumber = "REF-20260825-01",
                DepositDate = depositDate,
                DepositVarianceReason = null,
                DepositUploadedByUserId = 15,
                DepositUploadedByUser = uploader,
                DepositUploadedAt = uploadedAt
            };
            context.SalesReports.Add(report);
            await context.SaveChangesAsync();

            var controller = new SalesReportsController(context, null!, null!);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "15"),
                new(ClaimTypes.Role, "Owner")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            };

            var result = await controller.Review(105);
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("ReviewManager", viewResult.ViewName);

            var model = Assert.IsType<SalesReportReviewViewModel>(viewResult.Model);
            Assert.Equal(105, model.SalesReportId);
            Assert.Equal("/SalesReports/DepositSlip/slip_105.jpg", model.DepositSlipImageUrl);
            Assert.Equal(65000.00m, model.DepositedAmount);
            Assert.Equal("BDO", model.DepositBankName);
            Assert.Equal("REF-20260825-01", model.DepositReferenceNumber);
            Assert.Equal(depositDate, model.DepositDate);
            Assert.Null(model.DepositVarianceReason);
            Assert.Equal("Reviewer Sarah", model.DepositUploadedByName);
            Assert.Equal(uploadedAt, model.DepositUploadedAt);
            Assert.True(model.HasDepositSlip);
            Assert.Equal(0.00m, model.DepositVariance);
            Assert.True(model.IsDepositMatched);
            Assert.False(model.IsDepositDiscrepancy);
            Assert.Equal(65000.00m, model.CombinedCashSales);

            // Verify variance mappings when deposited amount has discrepancy
            model.DepositedAmount = 64500.00m;
            model.DepositVarianceReason = "Bank transaction fee deducted at counter";
            model.DepositVariance = (model.DepositedAmount ?? 0m) - model.ConfirmedCashToHandover;
            model.IsDepositMatched = model.HasDepositSlip && Math.Abs(model.DepositVariance) < 0.01m;
            model.IsDepositDiscrepancy = model.HasDepositSlip && Math.Abs(model.DepositVariance) >= 0.01m;

            Assert.Equal(-500.00m, model.DepositVariance);
            Assert.False(model.IsDepositMatched);
            Assert.True(model.IsDepositDiscrepancy);
            Assert.Equal("Bank transaction fee deducted at counter", model.DepositVarianceReason);
        }

    }

    public class FakeDepositSlipOcrService : AuditCkDayo.Services.IDepositSlipOcrService
    {
        public AuditCkDayo.Services.DepositSlipOcrResult Result { get; set; } = new() { Success = true };

        public Task<AuditCkDayo.Services.DepositSlipOcrResult> ParseDepositSlipAsync(System.IO.Stream imageStream)
        {
            return Task.FromResult(Result);
        }
    }
}
