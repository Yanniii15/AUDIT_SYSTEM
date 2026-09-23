using System.Linq;
using AuditCkDayo.Data;
using Microsoft.EntityFrameworkCore;
using System;
using AuditCkDayo.Models;
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
    }
}
