using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AuditCkDayo.Data.AuditDbContext))]
    [Migration("20260813123000_RepairAuditPnlCategorySchema")]
    public partial class RepairAuditPnlCategorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `PnlCategories` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Section` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `IsActive` tinyint(1) NOT NULL DEFAULT TRUE,
    CONSTRAINT `PK_PnlCategories` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;
");

            migrationBuilder.Sql(@"
SET @audit_item_details_has_pnl_category_id = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'AuditItemDetails'
      AND COLUMN_NAME = 'PnlCategoryId'
);
SET @audit_item_details_add_pnl_category_id = IF(
    @audit_item_details_has_pnl_category_id = 0,
    'ALTER TABLE `AuditItemDetails` ADD COLUMN `PnlCategoryId` int NULL',
    'SELECT 1'
);
PREPARE audit_item_details_add_pnl_category_id_stmt FROM @audit_item_details_add_pnl_category_id;
EXECUTE audit_item_details_add_pnl_category_id_stmt;
DEALLOCATE PREPARE audit_item_details_add_pnl_category_id_stmt;
");

            migrationBuilder.Sql(@"
SET @pnl_categories_section_name_index_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'PnlCategories'
      AND INDEX_NAME = 'IX_PnlCategories_Section_Name'
);
SET @pnl_categories_add_section_name_index = IF(
    @pnl_categories_section_name_index_exists = 0,
    'CREATE UNIQUE INDEX `IX_PnlCategories_Section_Name` ON `PnlCategories` (`Section`, `Name`)',
    'SELECT 1'
);
PREPARE pnl_categories_add_section_name_index_stmt FROM @pnl_categories_add_section_name_index;
EXECUTE pnl_categories_add_section_name_index_stmt;
DEALLOCATE PREPARE pnl_categories_add_section_name_index_stmt;
");

            migrationBuilder.Sql(@"
SET @audit_item_details_pnl_category_id_index_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'AuditItemDetails'
      AND INDEX_NAME = 'IX_AuditItemDetails_PnlCategoryId'
);
SET @audit_item_details_add_pnl_category_id_index = IF(
    @audit_item_details_pnl_category_id_index_exists = 0,
    'CREATE INDEX `IX_AuditItemDetails_PnlCategoryId` ON `AuditItemDetails` (`PnlCategoryId`)',
    'SELECT 1'
);
PREPARE audit_item_details_add_pnl_category_id_index_stmt FROM @audit_item_details_add_pnl_category_id_index;
EXECUTE audit_item_details_add_pnl_category_id_index_stmt;
DEALLOCATE PREPARE audit_item_details_add_pnl_category_id_index_stmt;
");

            migrationBuilder.Sql(@"
SET @audit_item_details_pnl_category_fk_exists = (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'AuditItemDetails'
      AND CONSTRAINT_NAME = 'FK_AuditItemDetails_PnlCategories_PnlCategoryId'
      AND CONSTRAINT_TYPE = 'FOREIGN KEY'
);
SET @audit_item_details_add_pnl_category_fk = IF(
    @audit_item_details_pnl_category_fk_exists = 0,
    'ALTER TABLE `AuditItemDetails` ADD CONSTRAINT `FK_AuditItemDetails_PnlCategories_PnlCategoryId` FOREIGN KEY (`PnlCategoryId`) REFERENCES `PnlCategories` (`Id`) ON DELETE SET NULL',
    'SELECT 1'
);
PREPARE audit_item_details_add_pnl_category_fk_stmt FROM @audit_item_details_add_pnl_category_fk;
EXECUTE audit_item_details_add_pnl_category_fk_stmt;
DEALLOCATE PREPARE audit_item_details_add_pnl_category_fk_stmt;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
