using backend.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/test-db")]
    public class TestDbController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TestDbController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("dump")]
        public async Task<IActionResult> DumpDb()
        {
            var users = await _context.Users
                .Include(u => u.Profile)
                .Include(u => u.Wallet)
                .Select(u => new
                {
                    u.Email,
                    u.Role,
                    u.Status,
                    Nome = u.Profile != null ? u.Profile.Nome : "Sem Nome",
                    SaldoDisponivel = u.Wallet != null ? (decimal?)u.Wallet.SaldoDisponivel : null,
                    SaldoComprometido = u.Wallet != null ? (decimal?)u.Wallet.SaldoComprometido : null,
                    SaldoInvestido = u.Wallet != null ? (decimal?)u.Wallet.SaldoInvestido : null,
                    Rentabilidade = u.Wallet != null ? (decimal?)u.Wallet.Rentabilidade : null
                })
                .ToListAsync();

            var loans = await _context.LoanRequests
                .Select(l => new { l.Id, l.ValorSolicitado, l.Status, l.TipoEmprestimo, l.Periodicidade })
                .ToListAsync();

            var investments = await _context.Investments
                .Select(i => new { i.Id, i.InvestorId, i.LoanId, i.ValorInvestido, i.Status, i.CreatedAt })
                .ToListAsync();

            var fund = await _context.ProtectionFunds.ToListAsync();

            return Ok(new { users, loans, investments, fund });
        }
    }
}
