using backend.Application.DTOs;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/investment-products")]
    public class InvestmentProductController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InvestmentProductController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/investment-products
        [HttpGet]
        public async Task<IActionResult> GetActiveProducts()
        {
            var products = await _context.InvestmentProducts
                .Where(p => p.Ativo)
                .OrderBy(p => p.ValorMinimo)
                .ThenBy(p => p.RentabilidadeMensal)
                .Select(p => new InvestmentProductDto
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    RentabilidadeMensal = p.RentabilidadeMensal,
                    ValorMinimo = p.ValorMinimo,
                    Descricao = p.Descricao,
                    Condicoes = p.Condicoes,
                    Risco = p.Risco,
                    PrazoMinimo = p.PrazoMinimo,
                    Ativo = p.Ativo,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return Ok(products);
        }
    }
}
