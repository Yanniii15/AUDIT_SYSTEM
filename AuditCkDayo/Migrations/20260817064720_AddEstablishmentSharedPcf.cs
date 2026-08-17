using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class AddEstablishmentSharedPcf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DailyStartingFloat",
                table: "Establishments",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "PcfBalance",
                table: "Establishments",
                type: "decimal(12,2)",
                nullable: false,
                defaultValue: 0.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyStartingFloat",
                table: "Establishments");

            migrationBuilder.DropColumn(
                name: "PcfBalance",
                table: "Establishments");
        }
    }
}
