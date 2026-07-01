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

        // Listar todos os usuários do sistema com paginação, busca e filtros combinados
        [HttpGet]
        public async Task<IActionResult> GetAllUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? nome = null,
            [FromQuery] string? cpf = null,
            [FromQuery] string? email = null,
            [FromQuery] string? role = null,
            [FromQuery] string? status = null,
            [FromQuery] int? minScore = null,
            [FromQuery] int? maxScore = null)
        {
            var query = _context.Users
                .AsNoTracking()
                .Include(u => u.Profile)
                .AsQueryable();

            // Busca rápida inteligente (Nome, CPF, Email)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u => u.Email.ToLower().Contains(term) ||
                                         (u.Profile != null && u.Profile.Nome.ToLower().Contains(term)) ||
                                         (u.Profile != null && u.Profile.CPF.Contains(term)));
            }

            // Filtros específicos combinados com AND
            if (!string.IsNullOrWhiteSpace(nome))
            {
                var term = nome.Trim().ToLower();
                query = query.Where(u => u.Profile != null && u.Profile.Nome.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(cpf))
            {
                var term = cpf.Trim().Replace(".", "").Replace("-", "");
                query = query.Where(u => u.Profile != null && u.Profile.CPF.Replace(".", "").Replace("-", "").Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var term = email.Trim().ToLower();
                query = query.Where(u => u.Email.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u => u.Role == role);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(u => u.Status == status);
            }

            if (minScore.HasValue)
            {
                query = query.Where(u => u.Profile != null && u.Profile.ScoreAtual >= minScore.Value);
            }

            if (maxScore.HasValue)
            {
                query = query.Where(u => u.Profile != null && u.Profile.ScoreAtual <= maxScore.Value);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var items = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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

            var result = new PagedResultDto<object>
            {
                Items = items.Cast<object>().ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Ok(result);
        }

        // Listar usuários pendentes de análise (Status == "EmAnalise") com paginação e busca
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            var query = _context.Users
                .AsNoTracking()
                .Include(u => u.Profile)
                .Where(u => u.Status == "EmAnalise")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u => u.Email.ToLower().Contains(term) ||
                                         (u.Profile != null && u.Profile.Nome.ToLower().Contains(term)) ||
                                         (u.Profile != null && u.Profile.CPF.Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var items = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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

            var result = new PagedResultDto<object>
            {
                Items = items.Cast<object>().ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Ok(result);
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
                .AsNoTracking()
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
                ScoreHistory = scoreHistory,
                Rentabilidade = user.Wallet != null ? user.Wallet.Rentabilidade : (decimal?)null,
                ObservacaoAdministrativa = user.Wallet != null ? user.Wallet.ObservacaoAdministrativa : null
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
        public async Task<IActionResult> ApproveUser(Guid id, [FromBody] ApproveUserDto? dto)
        {
            var user = await _context.Users
                .Include(u => u.Documents)
                .Include(u => u.Wallet)
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

            // Se for investidor, configurar rentabilidade e observação
            if (user.Role.Equals("Investidor", StringComparison.OrdinalIgnoreCase))
            {
                if (user.Wallet == null)
                {
                    user.Wallet = new InvestorWallet
                    {
                        Id = user.Id,
                        SaldoDisponivel = 0,
                        SaldoInvestido = 0,
                        Rendimentos = 0,
                        PerfilInvestimento = "Conservador"
                    };
                    _context.InvestorWallets.Add(user.Wallet);
                }

                if (dto != null)
                {
                    decimal rentabilidadeDesejada = dto.Rentabilidade ?? 0;

                    // Buscar menor taxa de juros ativa nas regras de crédito
                    var menorTaxaAtiva = await _context.ScoreCreditRules
                        .Where(r => r.Ativo)
                        .Select(r => (decimal?)r.TaxaJuros)
                        .MinAsync();

                    if (menorTaxaAtiva == null)
                    {
                        return BadRequest(new { message = "Não há regras de crédito ativas cadastradas no sistema para validar o spread." });
                    }

                    var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                    decimal spreadMinimo = config?.SpreadMinimoPlataforma ?? 1.00m;

                    decimal limiteMaximo = menorTaxaAtiva.Value - spreadMinimo;

                    if (rentabilidadeDesejada > limiteMaximo)
                    {
                        return BadRequest(new { message = $"Operação bloqueada: A rentabilidade informada ({rentabilidadeDesejada:F2}%) excede o limite máximo permitido ({limiteMaximo:F2}%) definido pelo spread mínimo da plataforma ({spreadMinimo:F2}%) sob a menor taxa ativa de empréstimo ({menorTaxaAtiva.Value:F2}%)." });
                    }

                    user.Wallet.Rentabilidade = rentabilidadeDesejada;
                    user.Wallet.ObservacaoAdministrativa = dto.ObservacaoAdministrativa ?? string.Empty;
                }
                user.Wallet.DataAtualizacao = DateTime.UtcNow;
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
