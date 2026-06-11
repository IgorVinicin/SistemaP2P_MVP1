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
    [Route("api/admin/credit-rules")]
    [Authorize(Roles = "Administrador")]
    public class AdminCreditRuleController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminCreditRuleController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/admin/credit-rules
        [HttpGet]
        public async Task<IActionResult> GetRules()
        {
            var rules = await _context.ScoreCreditRules
                .OrderBy(r => r.ScoreMin)
                .ToListAsync();
            return Ok(rules);
        }

        // POST /api/admin/credit-rules
        [HttpPost]
        public async Task<IActionResult> CreateRule([FromBody] CreditRuleDto dto)
        {
            try
            {
                if (dto.ScoreMin < 0 || dto.ScoreMax > 1000 || dto.ScoreMin > dto.ScoreMax)
                {
                    return BadRequest(new { message = "Faixa de score inválida. O score deve estar entre 0 e 1000, e o mínimo não pode ser maior que o máximo." });
                }

                // Validar sobreposição de faixas se a regra estiver ativa
                if (dto.Ativo && await IsRangeOverlapping(dto.ScoreMin, dto.ScoreMax))
                {
                    return BadRequest(new { message = "Erro: A faixa de score informada se sobrepõe com uma regra já existente ativa." });
                }

                var rule = new ScoreCreditRule
                {
                    Id = Guid.NewGuid(),
                    ScoreMin = dto.ScoreMin,
                    ScoreMax = dto.ScoreMax,
                    LimiteEmprestimo = dto.LimiteEmprestimo,
                    TaxaJuros = dto.TaxaJuros,
                    NivelRisco = dto.NivelRisco,
                    Ativo = dto.Ativo
                };

                _context.ScoreCreditRules.Add(rule);
                await _context.SaveChangesAsync();

                return Ok(rule);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT /api/admin/credit-rules/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRule(Guid id, [FromBody] CreditRuleDto dto)
        {
            try
            {
                var rule = await _context.ScoreCreditRules.FindAsync(id);
                if (rule == null)
                {
                    return NotFound(new { message = "Regra não encontrada." });
                }

                if (dto.ScoreMin < 0 || dto.ScoreMax > 1000 || dto.ScoreMin > dto.ScoreMax)
                {
                    return BadRequest(new { message = "Faixa de score inválida." });
                }

                // Validar sobreposição se a regra editada estiver ativa
                if (dto.Ativo && await IsRangeOverlapping(dto.ScoreMin, dto.ScoreMax, id))
                {
                    return BadRequest(new { message = "Erro: A faixa de score informada se sobrepõe com uma regra já existente ativa." });
                }

                rule.ScoreMin = dto.ScoreMin;
                rule.ScoreMax = dto.ScoreMax;
                rule.LimiteEmprestimo = dto.LimiteEmprestimo;
                rule.TaxaJuros = dto.TaxaJuros;
                rule.NivelRisco = dto.NivelRisco;
                rule.Ativo = dto.Ativo;

                await _context.SaveChangesAsync();
                return Ok(rule);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE /api/admin/credit-rules/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRule(Guid id)
        {
            try
            {
                var rule = await _context.ScoreCreditRules.FindAsync(id);
                if (rule == null)
                {
                    return NotFound(new { message = "Regra não encontrada." });
                }

                _context.ScoreCreditRules.Remove(rule);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Regra excluída com sucesso." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/admin/credit-rules/config
        [HttpGet("config")]
        public async Task<IActionResult> GetConfig()
        {
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new CreditConfig
                {
                    Id = Guid.NewGuid(),
                    ScoreMinimoParaSolicitacao = 150
                };
                _context.CreditConfigs.Add(config);
                await _context.SaveChangesAsync();
            }

            return Ok(config);
        }

        // PUT /api/admin/credit-rules/config
        [HttpPut("config")]
        public async Task<IActionResult> UpdateConfig([FromBody] CreditConfigDto dto)
        {
            try
            {
                if (dto.ScoreMinimoParaSolicitacao < 0 || dto.ScoreMinimoParaSolicitacao > 1000)
                {
                    return BadRequest(new { message = "O score mínimo deve estar entre 0 e 1000." });
                }

                var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                if (config == null)
                {
                    config = new CreditConfig
                    {
                        Id = Guid.NewGuid()
                    };
                    _context.CreditConfigs.Add(config);
                }

                config.ScoreMinimoParaSolicitacao = dto.ScoreMinimoParaSolicitacao;
                config.PercentualMultaAtraso = dto.PercentualMultaAtraso;
                config.PercentualJurosDiario = dto.PercentualJurosDiario;
                config.DiasParaCobranca = dto.DiasParaCobranca;
                config.DiasParaCobrancaIntensiva = dto.DiasParaCobrancaIntensiva;
                config.DiasParaInadimplencia = dto.DiasParaInadimplencia;

                await _context.SaveChangesAsync();

                return Ok(config);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<bool> IsRangeOverlapping(int scoreMin, int scoreMax, Guid? excludeId = null)
        {
            return await _context.ScoreCreditRules
                .AnyAsync(r => r.Ativo &&
                               (excludeId == null || r.Id != excludeId) &&
                               scoreMin <= r.ScoreMax &&
                               scoreMax >= r.ScoreMin);
        }
    }
}
