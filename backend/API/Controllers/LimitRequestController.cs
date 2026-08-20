using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/limits")]
    [Authorize]
    public class LimitRequestController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LimitRequestController(AppDbContext context)
        {
            _context = context;
        }

        // POST /api/limits/request
        [HttpPost("request")]
        public async Task<IActionResult> RequestLimit([FromBody] LimitRequestDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            if (dto == null || dto.LimiteSolicitado <= 0)
            {
                return BadRequest(new { message = "Valor do limite solicitado inválido." });
            }

            // Verificar se o tomador tem solicitação de limite pendente
            var temPendente = await _context.LimitRequests
                .AnyAsync(r => r.UserId == userId && r.Status == "Pendente");

            if (temPendente)
            {
                return BadRequest(new { message = "Você já possui uma solicitação de aumento de limite pendente de análise." });
            }

            // Calcular limite atual
            var profile = await _context.UserProfiles.FindAsync(userId);
            if (profile == null)
            {
                return BadRequest(new { message = "Perfil de usuário não encontrado." });
            }

            decimal limiteAtual = 0;
            if (profile.LimitePersonalizado.HasValue && profile.LimitePersonalizado.Value > 0)
            {
                limiteAtual = profile.LimitePersonalizado.Value;
            }
            else
            {
                // Buscar regra de score
                var score = profile.ScoreAtual;
                var rule = await _context.ScoreCreditRules
                    .FirstOrDefaultAsync(r => r.Ativo && score >= r.ScoreMin && score <= r.ScoreMax);
                limiteAtual = rule?.LimiteEmprestimo ?? 0;
            }

            if (dto.LimiteSolicitado <= limiteAtual)
            {
                return BadRequest(new { message = $"O limite solicitado ({dto.LimiteSolicitado:C}) deve ser maior do que o seu limite atual ({limiteAtual:C})." });
            }

            var request = new LimitRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LimiteSolicitado = dto.LimiteSolicitado,
                LimiteAtualNoMomento = limiteAtual,
                Status = "Pendente",
                DataCriacao = DateTime.UtcNow
            };

            _context.LimitRequests.Add(request);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Solicitação de aumento de limite enviada com sucesso!", id = request.Id });
        }

        // GET /api/limits/my-requests
        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyRequests()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            var requests = await _context.LimitRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.DataCriacao)
                .Select(r => new
                {
                    r.Id,
                    r.LimiteSolicitado,
                    r.LimiteAtualNoMomento,
                    r.Status,
                    r.DataCriacao,
                    r.DataAnalise,
                    r.ObservacaoAdmin
                })
                .ToListAsync();

            return Ok(requests);
        }

        // ================= ADMINISTRATIVO =================

        // GET /api/limits/admin/pending
        [HttpGet("admin/pending")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> GetPendingLimitRequests(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            var query = _context.LimitRequests
                .AsNoTracking()
                .Include(r => r.User)
                .ThenInclude(u => u!.Profile)
                .Where(r => r.Status == "Pendente")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(r => (r.User != null && r.User.Email.ToLower().Contains(term)) ||
                                         (r.User != null && r.User.Profile != null && r.User.Profile.Nome.ToLower().Contains(term)) ||
                                         (r.User != null && r.User.Profile != null && r.User.Profile.CPF.Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var items = await query
                .OrderByDescending(r => r.DataCriacao)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    Nome = r.User != null && r.User.Profile != null ? r.User.Profile.Nome : string.Empty,
                    CPF = r.User != null && r.User.Profile != null ? r.User.Profile.CPF : string.Empty,
                    Score = r.User != null && r.User.Profile != null ? r.User.Profile.ScoreAtual : 0,
                    r.LimiteSolicitado,
                    r.LimiteAtualNoMomento,
                    r.DataCriacao
                })
                .ToListAsync();

            return Ok(new
            {
                items,
                totalCount,
                page,
                pageSize,
                totalPages
            });
        }

        // POST /api/limits/admin/{id}/approve
        [HttpPost("admin/{id}/approve")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> ApproveLimitRequest(Guid id, [FromBody] ApproveLimitDto dto)
        {
            var request = await _context.LimitRequests
                .Include(r => r.User)
                .ThenInclude(u => u!.Profile)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return NotFound(new { message = "Solicitação não encontrada." });
            }

            if (request.Status != "Pendente")
            {
                return BadRequest(new { message = "Esta solicitação já foi analisada." });
            }

            decimal valorAprovado = dto?.ValorAprovado ?? request.LimiteSolicitado;

            if (valorAprovado <= 0)
            {
                return BadRequest(new { message = "Valor aprovado inválido." });
            }

            request.Status = "Aprovado";
            request.DataAnalise = DateTime.UtcNow;
            request.ObservacaoAdmin = dto?.ObservacaoAdmin;

            // Atualizar o perfil do tomador com o novo limite
            if (request.User != null && request.User.Profile != null)
            {
                request.User.Profile.LimitePersonalizado = valorAprovado;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Solicitação de aumento de limite aprovada com sucesso!", limiteDefinido = valorAprovado });
        }

        // POST /api/limits/admin/{id}/reject
        [HttpPost("admin/{id}/reject")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> RejectLimitRequest(Guid id, [FromBody] RejectLimitDto dto)
        {
            var request = await _context.LimitRequests.FindAsync(id);
            if (request == null)
            {
                return NotFound(new { message = "Solicitação não encontrada." });
            }

            if (request.Status != "Pendente")
            {
                return BadRequest(new { message = "Esta solicitação já foi analisada." });
            }

            request.Status = "Rejeitado";
            request.DataAnalise = DateTime.UtcNow;
            request.ObservacaoAdmin = dto?.ObservacaoAdmin ?? "Solicitação recusada pela administração.";

            await _context.SaveChangesAsync();

            return Ok(new { message = "Solicitação de aumento de limite rejeitada com sucesso." });
        }
    }

    public class LimitRequestDto
    {
        public decimal LimiteSolicitado { get; set; }
    }

    public class ApproveLimitDto
    {
        public decimal? ValorAprovado { get; set; }
        public string? ObservacaoAdmin { get; set; }
    }

    public class RejectLimitDto
    {
        public string? ObservacaoAdmin { get; set; }
    }
}
