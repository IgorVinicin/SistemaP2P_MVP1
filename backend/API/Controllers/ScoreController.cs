using backend.Application.DTOs;
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
    [Route("api/score")]
    [Authorize]
    public class ScoreController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ScoreController(AppDbContext context)
        {
            _context = context;
        }

        // Retorna as perguntas ativas para o cliente responder
        [HttpGet("questions")]
        public async Task<IActionResult> GetActiveQuestions()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            bool completed = false;
            var userAnswersList = new System.Collections.Generic.List<object>();

            if (!string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var userId))
            {
                // Verifica se existem respostas salvas para este usuário no banco
                var answers = await _context.UserAnswers
                    .AsNoTracking()
                    .Where(ua => ua.UserId == userId)
                    .ToListAsync();

                if (answers.Any())
                {
                    completed = true;
                    userAnswersList = answers.Select(ua => (object)new
                    {
                        ua.QuestionId,
                        ua.OptionId
                    }).ToList();
                }
            }

            var questions = await _context.Questions
                .AsNoTracking()
                .Where(q => q.Ativa)
                .Select(q => new
                {
                    q.Id,
                    q.Titulo,
                    Options = q.Options.Select(o => new
                    {
                        o.Id,
                        o.Texto
                    })
                })
                .ToListAsync();

            return Ok(new
            {
                questions = questions,
                questionnaireCompleted = completed,
                userAnswers = userAnswersList
            });
        }

        // Submete as respostas e calcula o score
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitAnswers([FromBody] SubmitAnswersDto dto)
        {
            try
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
                    return NotFound(new { message = "Perfil de usuário não encontrado." });
                }

                // Regra 1: Impedir envio múltiplo
                if (user.Profile.QuestionnaireCompleted)
                {
                    return BadRequest(new { message = "Questionário já preenchido." });
                }

                // Regra 1.5: Impedir se o usuário possuir status Inadimplente
                var hasInadimplencia = await _context.LoanRequests
                    .AnyAsync(l => l.UserId == userId && l.Status == "Inadimplente");
                if (hasInadimplencia)
                {
                    return BadRequest(new { message = "Você possui pendências financeiras e precisa regularizar sua situação para utilizar novamente a plataforma." });
                }

                // Limpar respostas anteriores do usuário
                var existingAnswers = await _context.UserAnswers
                    .Where(ua => ua.UserId == userId)
                    .ToListAsync();
                _context.UserAnswers.RemoveRange(existingAnswers);

                int scoreTotal = 0;

                foreach (var submission in dto.Answers)
                {
                    // Validar se a opção pertence à questão e obter a pontuação
                    var option = await _context.AnswerOptions
                        .FirstOrDefaultAsync(o => o.Id == submission.OptionId && o.QuestionId == submission.QuestionId);

                    if (option == null)
                    {
                        return BadRequest(new { message = $"Opção {submission.OptionId} inválida para a questão {submission.QuestionId}." });
                    }

                    scoreTotal += option.Pontuacao;

                    var userAnswer = new UserAnswer
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        QuestionId = submission.QuestionId,
                        OptionId = submission.OptionId
                    };
                    _context.UserAnswers.Add(userAnswer);
                }

                // Ajustar o score para a escala de 0 a 1000 se necessário, ou somar os pontos diretamente.
                // Como exemplo, assumiremos que as pontuações cadastradas somam até 1000. 
                // Limitando o score ao teto de 1000 e piso de 0:
                if (scoreTotal > 1000) scoreTotal = 1000;
                if (scoreTotal < 0) scoreTotal = 0;

                var scoreAnterior = user.Profile.ScoreAtual;

                // Salvar o score calculado no perfil do usuário
                user.Profile.ScoreAtual = scoreTotal;
                user.Profile.QuestionnaireCompleted = true;

                // Registrar histórico de score
                _context.ScoreHistories.Add(new ScoreHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ScoreAnterior = scoreAnterior,
                    ScoreNovo = scoreTotal,
                    Motivo = "Questionário Inicial",
                    DataAlteracao = DateTime.UtcNow,
                    AdminId = Guid.Empty
                });

                await _context.SaveChangesAsync();

                // Obter classificação de risco
                string faixaRisco;
                if (scoreTotal <= 300)
                {
                    faixaRisco = "Alto risco";
                }
                else if (scoreTotal <= 600)
                {
                    faixaRisco = "Médio risco";
                }
                else
                {
                    faixaRisco = "Baixo risco";
                }

                return Ok(new
                {
                    userId = user.Id,
                    score = scoreTotal,
                    faixaRisco = faixaRisco
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
