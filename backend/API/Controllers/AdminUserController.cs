using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = "Administrador")]
    public class AdminUserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminUserController(AppDbContext context)
        {
            _context = context;
        }

        // Listar todos os usuários do sistema
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Include(u => u.Profile)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Role,
                    u.Status,
                    u.CreatedAt,
                    Nome = u.Profile != null ? u.Profile.Nome : string.Empty,
                    CPF = u.Profile != null ? u.Profile.CPF : string.Empty,
                    ScoreAtual = u.Profile != null ? u.Profile.ScoreAtual : 0
                })
                .ToListAsync();

            return Ok(users);
        }

        // Listar usuários pendentes de análise (Status == "EmAnalise")
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingUsers()
        {
            var pendingUsers = await _context.Users
                .Include(u => u.Profile)
                .Where(u => u.Status == "EmAnalise")
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Role,
                    u.Status,
                    u.CreatedAt,
                    Nome = u.Profile != null ? u.Profile.Nome : string.Empty,
                    CPF = u.Profile != null ? u.Profile.CPF : string.Empty,
                    ScoreAtual = u.Profile != null ? u.Profile.ScoreAtual : 0
                })
                .ToListAsync();

            return Ok(pendingUsers);
        }

        // PUT /api/admin/users/{id}/profile
        [HttpPut("{id}/profile")]
        public async Task<IActionResult> UpdateUserProfile(Guid id, [FromBody] AdminUpdateProfileDto dto)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile == null)
            {
                return NotFound(new { message = "Perfil de usuário não encontrado." });
            }

            profile.Nome = dto.Nome;
            profile.CPF = dto.CPF;
            profile.Telefone = dto.Telefone;
            if (DateTime.TryParse(dto.DataNascimento, out var dt))
            {
                profile.DataNascimento = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Perfil atualizado com sucesso." });
        }

        // PUT /api/admin/users/{id}/status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] string status)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { message = "Usuário não encontrado." });
            }

            user.Status = status;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Status atualizado com sucesso." });
        }

        // Visualizar detalhes completos do usuário
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(Guid id)
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .Include(u => u.Company)
                .Include(u => u.Documents)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { message = "Usuário não encontrado." });
            }

            var answers = await _context.UserAnswers
                .Where(ua => ua.UserId == id)
                .Include(ua => ua.Question)
                .Include(ua => ua.Option)
                .Select(ua => new UserAnswerDetailDto
                {
                    Pergunta = ua.Question != null ? ua.Question.Titulo : string.Empty,
                    RespostaSelecionada = ua.Option != null ? ua.Option.Texto : string.Empty,
                    Pontuacao = ua.Option != null ? ua.Option.Pontuacao : 0
                })
                .ToListAsync();

            var loans = await _context.LoanRequests
                .Where(l => l.UserId == id)
                .OrderByDescending(l => l.DataCriacao)
                .Select(l => new UserLoanDetailDto
                {
                    Id = l.Id,
                    ValorSolicitado = l.ValorSolicitado,
                    ValorTotal = l.ValorTotal,
                    QuantidadeParcelas = l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas,
                    Status = l.Status,
                    DataSolicitacao = l.DataSolicitacao,
                    DataAprovacao = l.DataAprovacao,
                    DataEncerramento = l.DataEncerramento
                })
                .ToListAsync();

            var scoreHistory = await _context.ScoreHistories
                .Where(sh => sh.UserId == id)
                .OrderByDescending(sh => sh.DataAlteracao)
                .Select(sh => new UserScoreHistoryDto
                {
                    DataAlteracao = sh.DataAlteracao,
                    ScoreAnterior = sh.ScoreAnterior,
                    ScoreNovo = sh.ScoreNovo,
                    Motivo = sh.Motivo,
                    AdminEmail = _context.Users
                        .Where(u => u.Id == sh.AdminId)
                        .Select(u => u.Email)
                        .FirstOrDefault() ?? "Admin"
                })
                .ToListAsync();

            var detail = new UserDetailDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                Profile = user.Profile != null ? new ProfileDetailDto
                {
                    Nome = user.Profile.Nome,
                    CPF = user.Profile.CPF,
                    Telefone = user.Profile.Telefone,
                    DataNascimento = user.Profile.DataNascimento,
                    ScoreAtual = user.Profile.ScoreAtual,
                    PossuiEmpresa = user.Profile.PossuiEmpresa
                } : null,
                Company = user.Company != null ? new CompanyDetailDto
                {
                    RazaoSocial = user.Company.RazaoSocial,
                    NomeFantasia = user.Company.NomeFantasia,
                    CNPJ = user.Company.CNPJ,
                    TempoEmpresa = user.Company.TempoEmpresa,
                    FaturamentoMensal = user.Company.FaturamentoMensal
                } : null,
                Documents = user.Documents.Select(d => new DocumentDetailDto
                {
                    Id = d.Id,
                    TipoDocumento = d.TipoDocumento,
                    Status = d.Status,
                    CreatedAt = d.CreatedAt
                }).ToList(),
                Answers = answers,
                Loans = loans,
                ScoreHistory = scoreHistory
            };

            return Ok(detail);
        }

        // Visualizar/Obter o arquivo físico do documento
        [HttpGet("{id}/documents/{documentId}")]
        public async Task<IActionResult> GetUserDocument(Guid id, Guid documentId)
        {
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == id);

            if (document == null)
            {
                return NotFound(new { message = "Documento não encontrado." });
            }

            if (!System.IO.File.Exists(document.CaminhoArquivo))
            {
                return NotFound(new { message = "Arquivo físico do documento não encontrado no servidor." });
            }

            var contentType = "application/octet-stream";
            var fileExtension = Path.GetExtension(document.CaminhoArquivo).ToLower();

            if (fileExtension == ".jpg" || fileExtension == ".jpeg")
            {
                contentType = "image/jpeg";
            }
            else if (fileExtension == ".png")
            {
                contentType = "image/png";
            }
            else if (fileExtension == ".pdf")
            {
                contentType = "application/pdf";
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(document.CaminhoArquivo);
            return File(fileBytes, contentType, Path.GetFileName(document.CaminhoArquivo));
        }

        // Aprovar Usuário
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveUser(Guid id)
        {
            var user = await _context.Users
                .Include(u => u.Documents)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { message = "Usuário não encontrado." });
            }

            user.Status = "Aprovado";

            // Atualiza o status de todos os documentos vinculados para Aprovado
            foreach (var doc in user.Documents)
            {
                doc.Status = "Aprovado";
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Usuário e documentos aprovados com sucesso.", status = user.Status });
        }

        // Reprovar Usuário
        [HttpPost("{id}/reprove")]
        public async Task<IActionResult> ReproveUser(Guid id)
        {
            var user = await _context.Users
                .Include(u => u.Documents)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { message = "Usuário não encontrado." });
            }

            user.Status = "Reprovado";

            // Atualiza o status de todos os documentos vinculados para Reprovado
            foreach (var doc in user.Documents)
            {
                doc.Status = "Reprovado";
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Usuário e documentos reprovados com sucesso.", status = user.Status });
        }

        // PUT /api/admin/users/{id}/score
        [HttpPut("{id}/score")]
        public async Task<IActionResult> UpdateUserScore(Guid id, [FromBody] UpdateScoreDto dto)
        {
            try
            {
                var adminIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminIdString) || !Guid.TryParse(adminIdString, out var adminId))
                {
                    return Unauthorized(new { message = "Administrador não autenticado." });
                }

                var user = await _context.Users
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null || user.Profile == null)
                {
                    return NotFound(new { message = "Perfil de usuário não encontrado." });
                }

                int scoreAnterior = user.Profile.ScoreAtual;

                // Atualiza o score do usuário
                user.Profile.ScoreAtual = dto.NovoScore;

                // Registra auditoria
                var history = new ScoreHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = id,
                    ScoreAnterior = scoreAnterior,
                    ScoreNovo = dto.NovoScore,
                    Motivo = dto.Motivo,
                    DataAlteracao = DateTime.UtcNow,
                    AdminId = adminId
                };

                _context.ScoreHistories.Add(history);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Score atualizado com sucesso.", novoScore = dto.NovoScore });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
