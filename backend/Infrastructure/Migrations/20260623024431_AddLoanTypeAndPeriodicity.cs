using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanTypeAndPeriodicity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Periodicidade",
                table: "LoanRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoEmprestimo",
                table: "LoanRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Periodicidade",
                table: "LoanRequests");

            migrationBuilder.DropColumn(
                name: "TipoEmprestimo",
                table: "LoanRequests");
        }
    }
}
