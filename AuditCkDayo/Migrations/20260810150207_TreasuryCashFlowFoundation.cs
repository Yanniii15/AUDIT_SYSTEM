using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditCkDayo.Migrations
{
    /// <inheritdoc />
    public partial class TreasuryCashFlowFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TreasuryCashFlows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TreasuryUserId = table.Column<int>(type: "int", nullable: false),
                    CashFlowDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartingBalance = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TotalCashIn = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    TotalCashOut = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    NetCashFlow = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreasuryCashFlows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreasuryCashFlows_Users_TreasuryUserId",
                        column: x => x.TreasuryUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CashFlowEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TreasuryCashFlowId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EstablishmentId = table.Column<int>(type: "int", nullable: true),
                    CostCenterId = table.Column<int>(type: "int", nullable: true),
                    RelatedUserId = table.Column<int>(type: "int", nullable: true),
                    SourceDocumentId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    Notes = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ConfirmedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlowEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashFlowEntries_CostCenters_CostCenterId",
                        column: x => x.CostCenterId,
                        principalTable: "CostCenters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashFlowEntries_DocumentRecords_SourceDocumentId",
                        column: x => x.SourceDocumentId,
                        principalTable: "DocumentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashFlowEntries_Establishments_EstablishmentId",
                        column: x => x.EstablishmentId,
                        principalTable: "Establishments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashFlowEntries_TreasuryCashFlows_TreasuryCashFlowId",
                        column: x => x.TreasuryCashFlowId,
                        principalTable: "TreasuryCashFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashFlowEntries_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashFlowEntries_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CashFlowEntries_Users_RelatedUserId",
                        column: x => x.RelatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowEntries_ConfirmedByUserId",
                table: "CashFlowEntries",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowEntries_CostCenterId",
                table: "CashFlowEntries",
                column: "CostCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowEntries_CreatedByUserId",
                table: "CashFlowEntries",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowEntries_EstablishmentId",
                table: "CashFlowEntries",
                column: "EstablishmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowEntries_RelatedUserId",
                table: "CashFlowEntries",
                column: "RelatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowEntries_SourceDocumentId",
                table: "CashFlowEntries",
                column: "SourceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowEntries_TreasuryCashFlowId",
                table: "CashFlowEntries",
                column: "TreasuryCashFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_TreasuryCashFlows_TreasuryUserId",
                table: "TreasuryCashFlows",
                column: "TreasuryUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashFlowEntries");

            migrationBuilder.DropTable(
                name: "TreasuryCashFlows");
        }
    }
}
