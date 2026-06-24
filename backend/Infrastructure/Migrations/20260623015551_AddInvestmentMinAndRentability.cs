using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentMinAndRentability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObservacaoAdministrativa",
                table: "InvestorWallets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Rentabilidade",
                table: "InvestorWallets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorMinimoInvestimento",
                table: "CreditConfigs",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObservacaoAdministrativa",
                table: "InvestorWallets");

            migrationBuilder.DropColumn(
                name: "Rentabilidade",
                table: "InvestorWallets");

            migrationBuilder.DropColumn(
                name: "ValorMinimoInvestimento",
                table: "CreditConfigs");
        }
    }
}
