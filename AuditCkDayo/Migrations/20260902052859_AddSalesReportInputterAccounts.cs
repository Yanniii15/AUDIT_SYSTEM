using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesReportInputterAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClosingInputtedByUserId",
                table: "SalesReports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpeningInputtedByUserId",
                table: "SalesReports",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesReports_ClosingInputtedByUserId",
                table: "SalesReports",
                column: "ClosingInputtedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReports_OpeningInputtedByUserId",
                table: "SalesReports",
                column: "OpeningInputtedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReports_Users_ClosingInputtedByUserId",
                table: "SalesReports",
                column: "ClosingInputtedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReports_Users_OpeningInputtedByUserId",
                table: "SalesReports",
                column: "OpeningInputtedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesReports_Users_ClosingInputtedByUserId",
                table: "SalesReports");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReports_Users_OpeningInputtedByUserId",
                table: "SalesReports");

            migrationBuilder.DropIndex(
                name: "IX_SalesReports_ClosingInputtedByUserId",
                table: "SalesReports");

            migrationBuilder.DropIndex(
                name: "IX_SalesReports_OpeningInputtedByUserId",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "ClosingInputtedByUserId",
                table: "SalesReports");

            migrationBuilder.DropColumn(
                name: "OpeningInputtedByUserId",
                table: "SalesReports");
        }
    }
}
