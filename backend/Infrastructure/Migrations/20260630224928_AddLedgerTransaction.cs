using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LedgerTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TipoMovimentacao = table.Column<int>(type: "integer", nullable: false),
                    ContaOrigem = table.Column<int>(type: "integer", nullable: false),
                    ContaDestino = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceType = table.Column<string>(type: "text", nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoanId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstallmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Observacao = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_ContaDestino",
                table: "LedgerTransactions",
                column: "ContaDestino");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_ContaOrigem",
                table: "LedgerTransactions",
                column: "ContaOrigem");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_ReferenceId",
                table: "LedgerTransactions",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_TransactionDate",
                table: "LedgerTransactions",
                column: "TransactionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LedgerTransactions");
        }
    }
}
