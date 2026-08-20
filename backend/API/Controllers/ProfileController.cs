using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    public class UpdateProfileDto
    {
        public string? Telefone { get; set; }
    }

    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProfileController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/profile/me
        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            var user = await _context.Users
                .Include(u => u.Profile)
                .Include(u => u.Company)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { message = "Usuário não encontrado." });
            }

            return Ok(new
            {
                user.Id,
                user.Email,
                user.Role,
                user.Status,
                user.CreatedAt,
                Profile = user.Profile != null ? new
                {
                    user.Profile.Nome,
                    user.Profile.CPF,
                    user.Profile.DataNascimento,
                    user.Profile.Telefone,
                    user.Profile.PossuiEmpresa,
                    user.Profile.ScoreAtual,
                    user.Profile.LimitePersonalizado,
                    user.Profile.QuestionnaireCompleted
                } : null,
                Company = user.Company != null ? new
                {
                    user.Company.RazaoSocial,
                    user.Company.NomeFantasia,
                    user.Company.CNPJ,
                    user.Company.TempoEmpresa,
                    user.Company.FaturamentoMensal
                } : null
            });
        }

        // PUT /api/profile/me
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileDto dto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || user.Profile == null)
            {
                return NotFound(new { message = "Perfil não encontrado." });
            }

            if (!string.IsNullOrWhiteSpace(dto.Telefone))
            {
                user.Profile.Telefone = dto.Telefone.Trim();
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Perfil atualizado com sucesso." });
        }
    }
}
