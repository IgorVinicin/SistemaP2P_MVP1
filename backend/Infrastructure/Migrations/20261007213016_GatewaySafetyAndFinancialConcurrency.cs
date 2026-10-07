using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GatewaySafetyAndFinancialConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "JurosMoraCarregados",
                table: "LoanInstallments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "JurosMoraPagos",
                table: "LoanInstallments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "JurosPagos",
                table: "LoanInstallments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MultaCarregada",
                table: "LoanInstallments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MultaPaga",
                table: "LoanInstallments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrincipalDistribuido",
                table: "LoanInstallments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrincipalPago",
                table: "LoanInstallments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "LoanInstallments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Investments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "IdempotencyRecords",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DebtRollovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceInstallmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetInstallmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Principal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Interest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Penalty = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LateInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Actor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebtRollovers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GatewayCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    QrCodeBase64 = table.Column<string>(type: "text", nullable: true),
                    CopiaECola = table.Column<string>(type: "text", nullable: true),
                    InvoiceUrl = table.Column<string>(type: "text", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayCharges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GatewayTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<string>(type: "text", nullable: true),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PixKey = table.Column<string>(type: "text", nullable: false),
                    PixKeyType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayTransfers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GatewayWebhookEvents",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Event = table.Column<string>(type: "text", nullable: false),
                    ResourceId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatewayWebhookEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DebtRollovers_SourceInstallmentId",
                table: "DebtRollovers",
                column: "SourceInstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DebtRollovers_TargetInstallmentId",
                table: "DebtRollovers",
                column: "TargetInstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayCharges_Kind_ReferenceId",
                table: "GatewayCharges",
                columns: new[] { "Kind", "ReferenceId" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayCharges_Kind_ReferenceId_Status",
                table: "GatewayCharges",
                columns: new[] { "Kind", "ReferenceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GatewayCharges_PaymentId",
                table: "GatewayCharges",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatewayTransfers_Kind_ReferenceId",
                table: "GatewayTransfers",
                columns: new[] { "Kind", "ReferenceId" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_GatewayTransfers_TransferId",
                table: "GatewayTransfers",
                column: "TransferId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DebtRollovers");

            migrationBuilder.DropTable(
                name: "GatewayCharges");

            migrationBuilder.DropTable(
                name: "GatewayTransfers");

            migrationBuilder.DropTable(
                name: "GatewayWebhookEvents");

            migrationBuilder.DropColumn(
                name: "JurosMoraCarregados",
                table: "LoanInstallments");

            migrationBuilder.DropColumn(
                name: "JurosMoraPagos",
                table: "LoanInstallments");

            migrationBuilder.DropColumn(
                name: "JurosPagos",
                table: "LoanInstallments");

            migrationBuilder.DropColumn(
                name: "MultaCarregada",
                table: "LoanInstallments");

            migrationBuilder.DropColumn(
                name: "MultaPaga",
                table: "LoanInstallments");

            migrationBuilder.DropColumn(
                name: "PrincipalDistribuido",
                table: "LoanInstallments");

            migrationBuilder.DropColumn(
                name: "PrincipalPago",
                table: "LoanInstallments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "LoanInstallments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Investments");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "IdempotencyRecords");
        }
    }
}
