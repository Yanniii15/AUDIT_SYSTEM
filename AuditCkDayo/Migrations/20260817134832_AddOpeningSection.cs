using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningSection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBeerSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBeverageSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningCashSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningChangeAmount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningEaglesDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningEmployeeFivePercentDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningEmployeeTenPercentDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningFoodSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningGiftVoucherDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningGrossSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningLoyaltyCardDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OpeningNotes",
                table: "SalesReports",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningOtherSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningPcfFromSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningPwdDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OpeningReceiptNumberEnd",
                table: "SalesReports",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "OpeningReceiptNumberStart",
                table: "SalesReports",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningRestoPcf",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningSalesOverageAmount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OpeningSalesOverageReason",
                table: "SalesReports",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningSalesShortageAmount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OpeningSalesShortageReason",
                table: "SalesReports",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningSeniorDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OpeningWitnessName",
                table: "SalesReports",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Section",
                table: "SalesReportLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Section",
                table: "CashBreakdownLines",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpeningBeerSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningBeverageSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningCashSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningChangeAmount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningEaglesDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningEmployeeFivePercentDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningEmployeeTenPercentDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningFoodSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningGiftVoucherDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningGrossSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningLoyaltyCardDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningNotes",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningOtherSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningPcfFromSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningPwdDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningReceiptNumberEnd",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningReceiptNumberStart",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningRestoPcf",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningSalesOverageAmount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningSalesOverageReason",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningSalesShortageAmount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningSalesShortageReason",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningSeniorDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningWitnessName",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "SalesReportLines");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "CashBreakdownLines");
        }
    }
}
