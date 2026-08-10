using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class ClassificationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTreasury",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Establishments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMiscellaneous",
                table: "Establishments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOperatingBranch",
                table: "Establishments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "AllocationNotes",
                table: "AuditItemDetails",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "AssignedEstablishmentId",
                table: "AuditItemDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostCenterId",
                table: "AuditItemDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptStatus",
                table: "AuditItemDetails",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "HasReceipt")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CostCenters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCenters", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AuditItemDetails_AssignedEstablishmentId",
                table: "AuditItemDetails",
                column: "AssignedEstablishmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditItemDetails_CostCenterId",
                table: "AuditItemDetails",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenters_Name",
                table: "CostCenters",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditItemDetails_CostCenters_CostCenterId",
                table: "AuditItemDetails",
                column: "CostCenterId",
                principalTable: "CostCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditItemDetails_Establishments_AssignedEstablishmentId",
                table: "AuditItemDetails",
                column: "AssignedEstablishmentId",
                principalTable: "Establishments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditItemDetails_CostCenters_CostCenterId",
                table: "AuditItemDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditItemDetails_Establishments_AssignedEstablishmentId",
                table: "AuditItemDetails");

            migrationBuilder.DropTable(
                name: "CostCenters");

            migrationBuilder.DropIndex(
                name: "IX_AuditItemDetails_AssignedEstablishmentId",
                table: "AuditItemDetails");

            migrationBuilder.DropIndex(
                name: "IX_AuditItemDetails_CostCenterId",
                table: "AuditItemDetails");

            migrationBuilder.DropColumn(
                name: "IsTreasury",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Establishments");

            migrationBuilder.DropColumn(
                name: "IsMiscellaneous",
                table: "Establishments");

            migrationBuilder.DropColumn(
                name: "IsOperatingBranch",
                table: "Establishments");

            migrationBuilder.DropColumn(
                name: "AllocationNotes",
                table: "AuditItemDetails");

            migrationBuilder.DropColumn(
                name: "AssignedEstablishmentId",
                table: "AuditItemDetails");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "AuditItemDetails");

            migrationBuilder.DropColumn(
                name: "ReceiptStatus",
                table: "AuditItemDetails");
        }
    }
}
