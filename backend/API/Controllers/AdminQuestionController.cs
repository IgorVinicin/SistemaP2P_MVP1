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
    [Route("api/admin/questions")]
    [Authorize(Roles = "Administrador")]
    public class AdminQuestionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminQuestionController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllQuestions()
        {
            var questions = await _context.Questions
                .Select(q => new
                {
                    q.Id,
                    q.Titulo,
                    q.Ativa,
                    Options = q.Options.Select(o => new
                    {
                        o.Id,
                        o.QuestionId,
                        o.Texto,
                        o.Pontuacao
                    }).ToList()
                })
                .ToListAsync();
            return Ok(questions);
        }

        [HttpPost]
        public async Task<IActionResult> CreateQuestion([FromBody] CreateQuestionDto dto)
        {
            try
            {
                var question = new Question
                {
                    Id = Guid.NewGuid(),
                    Titulo = dto.Titulo,
                    Ativa = dto.Ativa
                };

                foreach (var optDto in dto.Options)
                {
                    question.Options.Add(new AnswerOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = question.Id,
                        Texto = optDto.Texto,
                        Pontuacao = optDto.Pontuacao
                    });
                }

                _context.Questions.Add(question);
                await _context.SaveChangesAsync();

                var result = new
                {
                    question.Id,
                    question.Titulo,
                    question.Ativa,
                    Options = question.Options.Select(o => new
                    {
                        o.Id,
                        o.QuestionId,
                        o.Texto,
                        o.Pontuacao
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQuestion(Guid id, [FromBody] CreateQuestionDto dto)
        {
            try
            {
                var question = await _context.Questions
                    .Include(q => q.Options)
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (question == null)
                {
                    return NotFound(new { message = "Questão não encontrada." });
                }

                question.Titulo = dto.Titulo;
                question.Ativa = dto.Ativa;

                // Remover as opções antigas
                _context.AnswerOptions.RemoveRange(question.Options);
                question.Options.Clear();

                // Inserir as novas opções
                if (dto.Options != null)
                {
                    foreach (var optDto in dto.Options)
                    {
                        question.Options.Add(new AnswerOption
                        {
                            Id = Guid.NewGuid(),
                            QuestionId = question.Id,
                            Texto = optDto.Texto,
                            Pontuacao = optDto.Pontuacao
                        });
                    }
                }

                await _context.SaveChangesAsync();

                var result = new
                {
                    question.Id,
                    question.Titulo,
                    question.Ativa,
                    Options = question.Options.Select(o => new
                    {
                        o.Id,
                        o.QuestionId,
                        o.Texto,
                        o.Pontuacao
                    }).ToList()
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteQuestion(Guid id)
        {
            try
            {
                var question = await _context.Questions.FindAsync(id);
                if (question == null)
                {
                    return NotFound(new { message = "Questão não encontrada." });
                }

                // Encontrar os IDs das opções desta pergunta
                var optionIds = await _context.AnswerOptions
                    .Where(o => o.QuestionId == id)
                    .Select(o => o.Id)
                    .ToListAsync();

                // Remover todas as respostas de usuários vinculadas à questão ou a qualquer uma de suas opções
                var userAnswers = await _context.UserAnswers
                    .Where(ua => ua.QuestionId == id || optionIds.Contains(ua.OptionId))
                    .ToListAsync();
                _context.UserAnswers.RemoveRange(userAnswers);

                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Questão deletada com sucesso." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/questions/{id}/options
        [HttpPost("{id}/options")]
        public async Task<IActionResult> AddOption(Guid id, [FromBody] CreateOptionDto dto)
        {
            try
            {
                var question = await _context.Questions.FindAsync(id);
                if (question == null)
                {
                    return NotFound(new { message = "Questão não encontrada." });
                }

                var option = new AnswerOption
                {
                    Id = Guid.NewGuid(),
                    QuestionId = id,
                    Texto = dto.Texto,
                    Pontuacao = dto.Pontuacao
                };

                _context.AnswerOptions.Add(option);
                await _context.SaveChangesAsync();

                return Ok(option);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // PUT /api/admin/options/{id}
        [HttpPut("/api/admin/options/{id}")]
        public async Task<IActionResult> UpdateOption(Guid id, [FromBody] CreateOptionDto dto)
        {
            try
            {
                var option = await _context.AnswerOptions.FindAsync(id);
                if (option == null)
                {
                    return NotFound(new { message = "Opção não encontrada." });
                }

                option.Texto = dto.Texto;
                option.Pontuacao = dto.Pontuacao;

                await _context.SaveChangesAsync();
                return Ok(option);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // DELETE /api/admin/options/{id}
        [HttpDelete("/api/admin/options/{id}")]
        public async Task<IActionResult> DeleteOption(Guid id)
        {
            try
            {
                var option = await _context.AnswerOptions.FindAsync(id);
                if (option == null)
                {
                    return NotFound(new { message = "Opção não encontrada." });
                }

                // Remover respostas de usuários associadas a esta opção antes de deletar
                var userAnswers = await _context.UserAnswers
                    .Where(ua => ua.OptionId == id)
                    .ToListAsync();
                _context.UserAnswers.RemoveRange(userAnswers);

                _context.AnswerOptions.Remove(option);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Opção deletada com sucesso." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
