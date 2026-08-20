using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDualRentabilidadeToInvestorWallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RentabilidadeSaldoDisponivel",
                table: "InvestorWallets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 1.00m);

            migrationBuilder.AddColumn<decimal>(
                name: "RentabilidadeSaldoComprometido",
                table: "InvestorWallets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RentabilidadeSaldoDisponivel",
                table: "InvestorWallets");

            migrationBuilder.DropColumn(
                name: "RentabilidadeSaldoComprometido",
                table: "InvestorWallets");
        }
    }
}
