using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class PcfReleaseAndAuditSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PcfReleases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReleasedByTreasuryUserId = table.Column<int>(type: "int", nullable: false),
                    ReceiverUserId = table.Column<int>(type: "int", nullable: true),
                    ReceiverName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EstablishmentId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CashFlowEntryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PcfReleases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcfReleases_CashFlowEntries_CashFlowEntryId",
                        column: x => x.CashFlowEntryId,
                        principalTable: "CashFlowEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PcfReleases_Establishments_EstablishmentId",
                        column: x => x.EstablishmentId,
                        principalTable: "Establishments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PcfReleases_Users_ReceiverUserId",
                        column: x => x.ReceiverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PcfReleases_Users_ReleasedByTreasuryUserId",
                        column: x => x.ReleasedByTreasuryUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AuditSettlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PcfReleaseId = table.Column<int>(type: "int", nullable: true),
                    ReceiverUserId = table.Column<int>(type: "int", nullable: true),
                    ReceiverName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponsibleManagerId = table.Column<int>(type: "int", nullable: false),
                    ProcessedByUserId = table.Column<int>(type: "int", nullable: false),
                    TotalPCReleased = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TotalAcceptedExpenses = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ExpectedChange = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ActualChangeReturned = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ShortOverAmount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditSettlements_PcfReleases_PcfReleaseId",
                        column: x => x.PcfReleaseId,
                        principalTable: "PcfReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditSettlements_Users_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditSettlements_Users_ReceiverUserId",
                        column: x => x.ReceiverUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditSettlements_Users_ResponsibleManagerId",
                        column: x => x.ResponsibleManagerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AuditSettlements_PcfReleaseId",
                table: "AuditSettlements",
                column: "PcfReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditSettlements_ProcessedByUserId",
                table: "AuditSettlements",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditSettlements_ReceiverUserId",
                table: "AuditSettlements",
                column: "ReceiverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditSettlements_ResponsibleManagerId",
                table: "AuditSettlements",
                column: "ResponsibleManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_PcfReleases_CashFlowEntryId",
                table: "PcfReleases",
                column: "CashFlowEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PcfReleases_EstablishmentId",
                table: "PcfReleases",
                column: "EstablishmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PcfReleases_ReceiverUserId",
                table: "PcfReleases",
                column: "ReceiverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PcfReleases_ReleasedByTreasuryUserId",
                table: "PcfReleases",
                column: "ReleasedByTreasuryUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditSettlements");

            migrationBuilder.DropTable(
                name: "PcfReleases");
        }
    }
}
