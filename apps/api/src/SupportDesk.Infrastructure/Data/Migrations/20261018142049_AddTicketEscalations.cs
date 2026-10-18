using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketEscalations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketEscalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    FromPriority = table.Column<int>(type: "int", nullable: false),
                    ToPriority = table.Column<int>(type: "int", nullable: false),
                    FromAgentId = table.Column<int>(type: "int", nullable: true),
                    ToAgentId = table.Column<int>(type: "int", nullable: true),
                    FromDueAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    ToDueAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EscalatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EscalatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketEscalations_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_TicketId",
                table: "TicketEscalations",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketEscalations");
        }
    }
}
