using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditPnlCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PnlCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Section = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PnlCategories", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "PnlCategoryId",
                table: "AuditItemDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PnlCategoryName",
                table: "AuditItemDetails",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Other")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PnlSection",
                table: "AuditItemDetails",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Other")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AuditItemDetails_PnlCategoryId",
                table: "AuditItemDetails",
                column: "PnlCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PnlCategories_Section_Name",
                table: "PnlCategories",
                columns: new[] { "Section", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditItemDetails_PnlCategories_PnlCategoryId",
                table: "AuditItemDetails",
                column: "PnlCategoryId",
                principalTable: "PnlCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditItemDetails_PnlCategories_PnlCategoryId",
                table: "AuditItemDetails");

            migrationBuilder.DropIndex(
                name: "IX_AuditItemDetails_PnlCategoryId",
                table: "AuditItemDetails");

            migrationBuilder.DropColumn(
                name: "PnlCategoryName",
                table: "AuditItemDetails");

            migrationBuilder.DropColumn(
                name: "PnlSection",
                table: "AuditItemDetails");

            migrationBuilder.DropColumn(
                name: "PnlCategoryId",
                table: "AuditItemDetails");

            migrationBuilder.DropTable(
                name: "PnlCategories");
        }
    }
}
