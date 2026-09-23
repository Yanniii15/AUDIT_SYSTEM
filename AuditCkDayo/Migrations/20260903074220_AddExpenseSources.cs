using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpenseSourceId",
                table: "AuditItemDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpenseSourceName",
                table: "AuditItemDetails",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ExpenseSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseSources", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AuditItemDetails_ExpenseSourceId",
                table: "AuditItemDetails",
                column: "ExpenseSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseSources_Name",
                table: "ExpenseSources",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditItemDetails_ExpenseSources_ExpenseSourceId",
                table: "AuditItemDetails",
                column: "ExpenseSourceId",
                principalTable: "ExpenseSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditItemDetails_ExpenseSources_ExpenseSourceId",
                table: "AuditItemDetails");

            migrationBuilder.DropTable(
                name: "ExpenseSources");

            migrationBuilder.DropIndex(
                name: "IX_AuditItemDetails_ExpenseSourceId",
                table: "AuditItemDetails");

            migrationBuilder.DropColumn(
                name: "ExpenseSourceId",
                table: "AuditItemDetails");

            migrationBuilder.DropColumn(
                name: "ExpenseSourceName",
                table: "AuditItemDetails");
        }
    }
}
