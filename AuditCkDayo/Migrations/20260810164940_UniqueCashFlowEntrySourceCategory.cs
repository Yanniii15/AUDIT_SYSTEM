using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class UniqueCashFlowEntrySourceCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashFlowEntries_SourceDocumentId",
                table: "CashFlowEntries");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowEntries_SourceDocumentId_Category",
                table: "CashFlowEntries",
                columns: new[] { "SourceDocumentId", "Category" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashFlowEntries_SourceDocumentId_Category",
                table: "CashFlowEntries");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowEntries_SourceDocumentId",
                table: "CashFlowEntries",
                column: "SourceDocumentId");
        }
    }
}
