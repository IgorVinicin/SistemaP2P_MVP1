using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitaAumentoLimiteToLoanRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LimiteNoMomentoSolicitacao",
                table: "LoanRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SolicitaAumentoLimite",
                table: "LoanRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                    RequestPath = table.Column<string>(type: "text", nullable: false),
                    RequestMethod = table.Column<string>(type: "text", nullable: false),
                    ResponseBody = table.Column<string>(type: "text", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_IdempotencyKey",
                table: "IdempotencyRecords",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "LimiteNoMomentoSolicitacao",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "SolicitaAumentoLimite",
                table: "LoanRequests");
        }
    }
}
