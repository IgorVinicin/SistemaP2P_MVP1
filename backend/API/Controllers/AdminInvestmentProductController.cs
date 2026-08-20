using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/investment-products")]
    [Authorize(Roles = "Administrador")]
    public class AdminInvestmentProductController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminInvestmentProductController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/admin/investment-products
        [HttpGet]
        public async Task<IActionResult> GetAllProducts()
        {
            var products = await _context.InvestmentProducts
                .OrderByDescending(p => p.CreatedAt)
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

        // POST /api/admin/investment-products
        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] CreateInvestmentProductDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nome))
            {
                return BadRequest(new { message = "O nome do produto de investimento é obrigatório." });
            }

            if (dto.RentabilidadeMensal < 0)
            {
                return BadRequest(new { message = "A rentabilidade mensal não pode ser negativa." });
            }

            if (dto.ValorMinimo <= 0)
            {
                return BadRequest(new { message = "O valor mínimo deve ser positivo." });
            }

            var product = new InvestmentProduct
            {
                Id = Guid.NewGuid(),
                Nome = dto.Nome.Trim(),
                RentabilidadeMensal = dto.RentabilidadeMensal,
                ValorMinimo = dto.ValorMinimo,
                Descricao = dto.Descricao?.Trim() ?? string.Empty,
                Condicoes = dto.Condicoes?.Trim() ?? string.Empty,
                Risco = string.IsNullOrWhiteSpace(dto.Risco) ? "Baixo" : dto.Risco.Trim(),
                PrazoMinimo = string.IsNullOrWhiteSpace(dto.PrazoMinimo) ? "Livre" : dto.PrazoMinimo.Trim(),
                Ativo = dto.Ativo,
                CreatedAt = DateTime.UtcNow
            };

            _context.InvestmentProducts.Add(product);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAllProducts), new { id = product.Id }, new InvestmentProductDto
            {
                Id = product.Id,
                Nome = product.Nome,
                RentabilidadeMensal = product.RentabilidadeMensal,
                ValorMinimo = product.ValorMinimo,
                Descricao = product.Descricao,
                Condicoes = product.Condicoes,
                Risco = product.Risco,
                PrazoMinimo = product.PrazoMinimo,
                Ativo = product.Ativo,
                CreatedAt = product.CreatedAt
            });
        }

        // PUT /api/admin/investment-products/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateInvestmentProductDto dto)
        {
            var product = await _context.InvestmentProducts.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Produto de investimento não encontrado." });
            }

            if (string.IsNullOrWhiteSpace(dto.Nome))
            {
                return BadRequest(new { message = "O nome do produto de investimento é obrigatório." });
            }

            if (dto.RentabilidadeMensal < 0)
            {
                return BadRequest(new { message = "A rentabilidade mensal não pode ser negativa." });
            }

            if (dto.ValorMinimo <= 0)
            {
                return BadRequest(new { message = "O valor mínimo deve ser positivo." });
            }

            product.Nome = dto.Nome.Trim();
            product.RentabilidadeMensal = dto.RentabilidadeMensal;
            product.ValorMinimo = dto.ValorMinimo;
            product.Descricao = dto.Descricao?.Trim() ?? string.Empty;
            product.Condicoes = dto.Condicoes?.Trim() ?? string.Empty;
            product.Risco = string.IsNullOrWhiteSpace(dto.Risco) ? "Baixo" : dto.Risco.Trim();
            product.PrazoMinimo = string.IsNullOrWhiteSpace(dto.PrazoMinimo) ? "Livre" : dto.PrazoMinimo.Trim();
            product.Ativo = dto.Ativo;

            await _context.SaveChangesAsync();

            return Ok(new InvestmentProductDto
            {
                Id = product.Id,
                Nome = product.Nome,
                RentabilidadeMensal = product.RentabilidadeMensal,
                ValorMinimo = product.ValorMinimo,
                Descricao = product.Descricao,
                Condicoes = product.Condicoes,
                Risco = product.Risco,
                PrazoMinimo = product.PrazoMinimo,
                Ativo = product.Ativo,
                CreatedAt = product.CreatedAt
            });
        }

        // DELETE /api/admin/investment-products/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var product = await _context.InvestmentProducts.FindAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Produto de investimento não encontrado." });
            }

            _context.InvestmentProducts.Remove(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Produto de investimento removido com sucesso." });
        }
    }
}
