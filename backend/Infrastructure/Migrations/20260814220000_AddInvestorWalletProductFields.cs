using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestorWalletProductFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProdutoInvestimentoId",
                table: "InvestorWallets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProdutoInvestimentoNome",
                table: "InvestorWallets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ProdutoEscolhido",
                table: "InvestorWallets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PodeAlterarProduto",
                table: "InvestorWallets",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProdutoInvestimentoId",
                table: "InvestorWallets");

            migrationBuilder.DropColumn(
                name: "ProdutoInvestimentoNome",
                table: "InvestorWallets");

            migrationBuilder.DropColumn(
                name: "ProdutoEscolhido",
                table: "InvestorWallets");

            migrationBuilder.DropColumn(
                name: "PodeAlterarProduto",
                table: "InvestorWallets");
        }
    }
}
