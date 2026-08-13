using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignedReviewers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedReceiverId",
                table: "SurrenderRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedReviewerId",
                table: "AuditItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurrenderRequests_AssignedReceiverId",
                table: "SurrenderRequests",
                column: "AssignedReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditItems_AssignedReviewerId",
                table: "AuditItems",
                column: "AssignedReviewerId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditItems_Users_AssignedReviewerId",
                table: "AuditItems",
                column: "AssignedReviewerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SurrenderRequests_Users_AssignedReceiverId",
                table: "SurrenderRequests",
                column: "AssignedReceiverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditItems_Users_AssignedReviewerId",
                table: "AuditItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SurrenderRequests_Users_AssignedReceiverId",
                table: "SurrenderRequests");

            migrationBuilder.DropIndex(
                name: "IX_SurrenderRequests_AssignedReceiverId",
                table: "SurrenderRequests");

            migrationBuilder.DropIndex(
                name: "IX_AuditItems_AssignedReviewerId",
                table: "AuditItems");

            migrationBuilder.DropColumn(
                name: "AssignedReceiverId",
                table: "SurrenderRequests");

            migrationBuilder.DropColumn(
                name: "AssignedReviewerId",
                table: "AuditItems");
        }
    }
}
