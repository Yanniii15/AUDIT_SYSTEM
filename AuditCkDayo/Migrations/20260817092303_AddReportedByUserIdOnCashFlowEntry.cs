using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class AddReportedByUserIdOnCashFlowEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReportedByUserId",
                table: "CashFlowEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowEntries_ReportedByUserId",
                table: "CashFlowEntries",
                column: "ReportedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CashFlowEntries_Users_ReportedByUserId",
                table: "CashFlowEntries",
                column: "ReportedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashFlowEntries_Users_ReportedByUserId",
                table: "CashFlowEntries");

            migrationBuilder.DropIndex(
                name: "IX_CashFlowEntries_ReportedByUserId",
                table: "CashFlowEntries");

            migrationBuilder.DropColumn(
                name: "ReportedByUserId",
                table: "CashFlowEntries");
        }
    }
}
