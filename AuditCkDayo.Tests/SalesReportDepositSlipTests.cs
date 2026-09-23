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
    }
}
