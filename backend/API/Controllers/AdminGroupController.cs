using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Infrastructure;
using backend.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/groups")]
    [Authorize(Roles = "Administrador")]
    public class AdminGroupController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminGroupController(AppDbContext context)
        {
            _context = context;
        }

        public class CreateUpdateGroupDto
        {
            public string Nome { get; set; } = string.Empty;
            public string? Descricao { get; set; }
            public List<Guid> InvestorIds { get; set; } = new List<Guid>();
        }

        [HttpGet]
        public async Task<IActionResult> GetGroups()
        {
            var groups = await _context.InvestorGroups
                .Select(g => new
                {
                    g.Id,
                    g.Nome,
                    g.Descricao,
                    g.CreatedAt,
                    InvestorsCount = g.Members.Count,
                    Investors = g.Members.Select(m => new { 
                        m.InvestorId, 
                        Name = m.Investor.Profile.Nome ?? m.Investor.Email 
                    })
                })
                .ToListAsync();

            return Ok(groups);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGroup(Guid id)
        {
            var g = await _context.InvestorGroups
                .Include(g => g.Members)
                .ThenInclude(m => m.Investor)
                .ThenInclude(i => i.Profile)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (g == null) return NotFound(new { message = "Grupo não encontrado." });

            return Ok(new
            {
                g.Id,
                g.Nome,
                g.Descricao,
                g.CreatedAt,
                Investors = g.Members.Select(m => new { 
                    m.InvestorId, 
                    Name = m.Investor.Profile.Nome ?? m.Investor.Email 
                })
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] CreateUpdateGroupDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Nome)) return BadRequest(new { message = "O nome do grupo é obrigatório." });
                
                var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                var maxLimit = config?.MaxInvestidoresPorEmprestimo ?? 10;

                if (dto.InvestorIds != null && dto.InvestorIds.Count > maxLimit) return BadRequest(new { message = $"Um grupo não pode ter mais do que {maxLimit} investidores." });

                var group = new InvestorGroup
                {
                    Id = Guid.NewGuid(),
                    Nome = dto.Nome,
                    Descricao = dto.Descricao,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dto.InvestorIds ??= new List<Guid>();
                foreach (var invId in dto.InvestorIds)
                {
                    group.Members.Add(new InvestorGroupMember { GroupId = group.Id, InvestorId = invId });
                }

                _context.InvestorGroups.Add(group);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Grupo criado com sucesso.", id = group.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro interno: {ex.Message} {ex.InnerException?.Message}" });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGroup(Guid id, [FromBody] CreateUpdateGroupDto dto)
        {
            try
            {
                var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                var maxLimit = config?.MaxInvestidoresPorEmprestimo ?? 10;

                if (dto.InvestorIds != null && dto.InvestorIds.Count > maxLimit) return BadRequest(new { message = $"Um grupo não pode ter mais do que {maxLimit} investidores." });

                var group = await _context.InvestorGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id);
                if (group == null) return NotFound(new { message = "Grupo não encontrado." });

                group.Nome = dto.Nome;
                group.Descricao = dto.Descricao;
                group.UpdatedAt = DateTime.UtcNow;

                _context.InvestorGroupMembers.RemoveRange(group.Members);
                group.Members.Clear();

                dto.InvestorIds ??= new List<Guid>();
                foreach (var invId in dto.InvestorIds)
                {
                    group.Members.Add(new InvestorGroupMember { GroupId = group.Id, InvestorId = invId });
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Grupo atualizado com sucesso." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro interno: {ex.Message} {ex.InnerException?.Message}" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGroup(Guid id)
        {
            var group = await _context.InvestorGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id);
            if (group == null) return NotFound(new { message = "Grupo não encontrado." });

            _context.InvestorGroupMembers.RemoveRange(group.Members);
            _context.InvestorGroups.Remove(group);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Grupo excluído com sucesso." });
        }
    }
}
