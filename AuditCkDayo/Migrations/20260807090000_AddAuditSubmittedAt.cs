using AuditCkDayo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AuditDbContext))]
    [Migration("20260807090000_AddAuditSubmittedAt")]
    public partial class AddAuditSubmittedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "AuditItems",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "AuditItems");
        }
    }
}
