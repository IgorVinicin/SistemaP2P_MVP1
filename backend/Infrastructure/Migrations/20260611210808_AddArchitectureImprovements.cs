using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArchitectureImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Investments_LoanRequests_LoanId",
                table: "Investments");

            migrationBuilder.DropForeignKey(
                name: "FK_Investments_Users_InvestorId",
                table: "Investments");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestorWallets_Users_Id",
                table: "InvestorWallets");

            migrationBuilder.DropForeignKey(
                name: "FK_LoanRequests_Users_UserId",
                table: "LoanRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAnswers_Users_UserId",
                table: "UserAnswers");

            migrationBuilder.AddColumn<bool>(
                name: "QuestionnaireCompleted",
                table: "UserProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalRecebido",
                table: "ProtectionFunds",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalUtilizado",
                table: "ProtectionFunds",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAnalise",
                table: "LoanRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAprovacao",
                table: "LoanRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataCriacao",
                table: "LoanRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DataEncerramento",
                table: "LoanRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSolicitacao",
                table: "LoanRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "NivelRisco",
                table: "LoanRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ObservacaoAdmin",
                table: "LoanRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Observacoes",
                table: "LoanRequests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "QuantidadeParcelas",
                table: "LoanRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ScoreNoMomento",
                table: "LoanRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxaJuros",
                table: "LoanRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxaJurosAplicada",
                table: "LoanRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorTotal",
                table: "LoanRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAtualizacao",
                table: "InvestorWallets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DataCriacao",
                table: "InvestorWallets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "PerfilInvestimento",
                table: "InvestorWallets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAportado",
                table: "InvestorWallets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalSacado",
                table: "InvestorWallets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CreditConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreMinimoParaSolicitacao = table.Column<int>(type: "integer", nullable: false),
                    PercentualMultaAtraso = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PercentualJurosDiario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DiasParaCobranca = table.Column<int>(type: "integer", nullable: false),
                    DiasParaCobrancaIntensiva = table.Column<int>(type: "integer", nullable: false),
                    DiasParaInadimplencia = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinancialEventHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsuarioResponsavel = table.Column<string>(type: "text", nullable: false),
                    Observacao = table.Column<string>(type: "text", nullable: false),
                    EventoExecutado = table.Column<string>(type: "text", nullable: false),
                    LoanRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialEventHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvestorTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoAnterior = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SaldoPosterior = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataMovimentacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    UsuarioResponsavel = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestorTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestorTransactions_Users_InvestorId",
                        column: x => x.InvestorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanInstallments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    NumeroParcela = table.Column<int>(type: "integer", nullable: false),
                    ValorParcela = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorPrincipal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorJuros = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataVencimento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ValorPago = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiasAtraso = table.Column<int>(type: "integer", nullable: false),
                    Multa = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    JurosMora = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanInstallments_LoanRequests_LoanRequestId",
                        column: x => x.LoanRequestId,
                        principalTable: "LoanRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScoreCreditRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreMin = table.Column<int>(type: "integer", nullable: false),
                    ScoreMax = table.Column<int>(type: "integer", nullable: false),
                    LimiteEmprestimo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxaJuros = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NivelRisco = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreCreditRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScoreHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreAnterior = table.Column<int>(type: "integer", nullable: false),
                    ScoreNovo = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: false),
                    DataAlteracao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanRequests_Status",
                table: "LoanRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialEventHistories_EventoExecutado_Data",
                table: "FinancialEventHistories",
                columns: new[] { "EventoExecutado", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestorTransactions_InvestorId_DataMovimentacao",
                table: "InvestorTransactions",
                columns: new[] { "InvestorId", "DataMovimentacao" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanInstallments_LoanRequestId",
                table: "LoanInstallments",
                column: "LoanRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanInstallments_Status_DataVencimento",
                table: "LoanInstallments",
                columns: new[] { "Status", "DataVencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreHistories_UserId",
                table: "ScoreHistories",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Investments_LoanRequests_LoanId",
                table: "Investments",
                column: "LoanId",
                principalTable: "LoanRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Investments_Users_InvestorId",
                table: "Investments",
                column: "InvestorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestorWallets_Users_Id",
                table: "InvestorWallets",
                column: "Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoanRequests_Users_UserId",
                table: "LoanRequests",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAnswers_Users_UserId",
                table: "UserAnswers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Investments_LoanRequests_LoanId",
                table: "Investments");

            migrationBuilder.DropForeignKey(
                name: "FK_Investments_Users_InvestorId",
                table: "Investments");

            migrationBuilder.DropForeignKey(
                name: "FK_InvestorWallets_Users_Id",
                table: "InvestorWallets");

            migrationBuilder.DropForeignKey(
                name: "FK_LoanRequests_Users_UserId",
                table: "LoanRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAnswers_Users_UserId",
                table: "UserAnswers");

            migrationBuilder.DropTable(
                name: "CreditConfigs");

            migrationBuilder.DropTable(
                name: "FinancialEventHistories");

            migrationBuilder.DropTable(
                name: "InvestorTransactions");

            migrationBuilder.DropTable(
                name: "LoanInstallments");

            migrationBuilder.DropTable(
                name: "ScoreCreditRules");

            migrationBuilder.DropTable(
                name: "ScoreHistories");

            migrationBuilder.DropIndex(
                name: "IX_LoanRequests_Status",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "QuestionnaireCompleted",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "TotalRecebido",
                table: "ProtectionFunds");

            migrationBuilder.DropColumn(
                name: "TotalUtilizado",
                table: "ProtectionFunds");

            migrationBuilder.DropColumn(
                name: "DataAnalise",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "DataAprovacao",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "DataCriacao",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "DataEncerramento",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "DataSolicitacao",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "NivelRisco",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "ObservacaoAdmin",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "Observacoes",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "QuantidadeParcelas",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "ScoreNoMomento",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "TaxaJuros",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "TaxaJurosAplicada",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "ValorTotal",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "DataAtualizacao",
                table: "InvestorWallets");

            migrationBuilder.DropColumn(
                name: "DataCriacao",
                table: "InvestorWallets");

            migrationBuilder.DropColumn(
                name: "PerfilInvestimento",
                table: "InvestorWallets");

            migrationBuilder.DropColumn(
                name: "TotalAportado",
                table: "InvestorWallets");

            migrationBuilder.DropColumn(
                name: "TotalSacado",
                table: "InvestorWallets");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UserId",
                table: "Documents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Investments_LoanRequests_LoanId",
                table: "Investments",
                column: "LoanId",
                principalTable: "LoanRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Investments_Users_InvestorId",
                table: "Investments",
                column: "InvestorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InvestorWallets_Users_Id",
                table: "InvestorWallets",
                column: "Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LoanRequests_Users_UserId",
                table: "LoanRequests",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAnswers_Users_UserId",
                table: "UserAnswers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
