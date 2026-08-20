using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPixFieldsToWithdrawalRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChavePix",
                table: "WithdrawalRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoChavePix",
                table: "WithdrawalRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChavePix",
                table: "WithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "TipoChavePix",
                table: "WithdrawalRequests");
        }
    }
}
