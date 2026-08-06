using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchVerificationRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstablishmentId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AuditItems",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "AwaitingBranchVerification",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pending")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EstablishmentId",
                table: "Users",
                column: "EstablishmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Establishments_EstablishmentId",
                table: "Users",
                column: "EstablishmentId",
                principalTable: "Establishments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Establishments_EstablishmentId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_EstablishmentId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EstablishmentId",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AuditItems",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "AwaitingBranchVerification")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
