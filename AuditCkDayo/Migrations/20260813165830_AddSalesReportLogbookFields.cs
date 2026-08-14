using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesReportLogbookFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BeerSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BeverageSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CashSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ChangeAmount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosingGrossSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EaglesDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeFivePercentDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployeeTenPercentDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FoodSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GiftVoucherDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyCardDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PcfFromSales",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PwdDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestoPcf",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesOverageAmount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SalesOverageReason",
                table: "SalesReports",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "SalesShortageAmount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SalesShortageReason",
                table: "SalesReports",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "SeniorDiscount",
                table: "SalesReports",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SalesReportLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SalesReportId = table.Column<int>(type: "int", nullable: false),
                    LineType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Label = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesReportLines_SalesReports_SalesReportId",
                        column: x => x.SalesReportId,
                        principalTable: "SalesReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReportLines_SalesReportId",
                table: "SalesReportLines",
                column: "SalesReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesReportLines");

            migrationBuilder.DropColumn(
                name: "BeerSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "BeverageSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "CashSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "ChangeAmount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "ClosingGrossSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "EaglesDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "EmployeeFivePercentDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "EmployeeTenPercentDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "FoodSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "GiftVoucherDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "LoyaltyCardDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OtherSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "PcfFromSales",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "PwdDiscount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "RestoPcf",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "SalesOverageAmount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "SalesOverageReason",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "SalesShortageAmount",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "SalesShortageReason",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "SeniorDiscount",
                table: "SalesReports");
        }
    }
}
