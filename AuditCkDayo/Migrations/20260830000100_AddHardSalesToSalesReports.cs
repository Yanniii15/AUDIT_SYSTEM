using AuditCkDayo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AuditDbContext))]
    [Migration("20260830000100_AddHardSalesToSalesReports")]
    public partial class AddHardSalesToSalesReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "SET @addHardSales = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SalesReports' AND COLUMN_NAME = 'HardSales') = 0, 'ALTER TABLE `SalesReports` ADD `HardSales` decimal(12,2) NOT NULL DEFAULT 0.0', 'SELECT 1'); " +
                "PREPARE addHardSalesStmt FROM @addHardSales; " +
                "EXECUTE addHardSalesStmt; " +
                "DEALLOCATE PREPARE addHardSalesStmt;");

            migrationBuilder.Sql(
                "SET @addOpeningHardSales = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SalesReports' AND COLUMN_NAME = 'OpeningHardSales') = 0, 'ALTER TABLE `SalesReports` ADD `OpeningHardSales` decimal(12,2) NOT NULL DEFAULT 0.0', 'SELECT 1'); " +
                "PREPARE addOpeningHardSalesStmt FROM @addOpeningHardSales; " +
                "EXECUTE addOpeningHardSalesStmt; " +
                "DEALLOCATE PREPARE addOpeningHardSalesStmt;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "SET @dropHardSales = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SalesReports' AND COLUMN_NAME = 'HardSales') = 1, 'ALTER TABLE `SalesReports` DROP COLUMN `HardSales`', 'SELECT 1'); " +
                "PREPARE dropHardSalesStmt FROM @dropHardSales; " +
                "EXECUTE dropHardSalesStmt; " +
                "DEALLOCATE PREPARE dropHardSalesStmt;");

            migrationBuilder.Sql(
                "SET @dropOpeningHardSales = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SalesReports' AND COLUMN_NAME = 'OpeningHardSales') = 1, 'ALTER TABLE `SalesReports` DROP COLUMN `OpeningHardSales`', 'SELECT 1'); " +
                "PREPARE dropOpeningHardSalesStmt FROM @dropOpeningHardSales; " +
                "EXECUTE dropOpeningHardSalesStmt; " +
                "DEALLOCATE PREPARE dropOpeningHardSalesStmt;");
        }
    }
}
