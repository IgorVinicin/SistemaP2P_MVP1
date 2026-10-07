using backend.API.Controllers;
using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Services;

public class ScoreSubmissionTests
{
    [Fact]
    public async Task DuplicateQuestionCannotInflateScore()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var question = new Question { Id = Guid.NewGuid(), Ativa = true, Titulo = "Pergunta" };
        context.Questions.Add(question);
        await context.SaveChangesAsync();
        var dto = new SubmitAnswersDto
        {
            Answers = new List<AnswerSubmissionDto>
            {
                new() { QuestionId = question.Id, OptionId = Guid.NewGuid() },
                new() { QuestionId = question.Id, OptionId = Guid.NewGuid() }
            }
        };
        Assert.IsType<BadRequestObjectResult>(await new ScoreController(context).SubmitAnswers(dto));
        Assert.Empty(context.UserAnswers);
        Assert.Empty(context.ScoreHistories);
    }

    [Fact]
    public async Task IncompleteFormDoesNotCompleteQuestionnaire()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var question = new Question { Id = Guid.NewGuid(), Ativa = true, Titulo = "Uma pergunta" };
        context.Questions.AddRange(question, new Question { Id = Guid.NewGuid(), Ativa = true, Titulo = "Outra pergunta" });
        await context.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await new ScoreController(context).SubmitAnswers(new SubmitAnswersDto
        {
            Answers = new List<AnswerSubmissionDto> { new() { QuestionId = question.Id, OptionId = Guid.NewGuid() } }
        }));
        Assert.Empty(context.UserAnswers);
    }
}
