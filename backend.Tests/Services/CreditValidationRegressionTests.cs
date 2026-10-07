using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Services;

public class CreditValidationRegressionTests
{
    [Theory]
    [InlineData("Financiado")]
    [InlineData("EmCaptacao")]
    [InlineData("EmPagamento")]
    [InlineData("Inadimplente")]
    public async Task AllCommittedLoansBlockEarlyRenewal(string status)
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.LoanRequests.Add(new LoanRequest { Id = Guid.NewGuid(), UserId = userId, Status = status, ValorSolicitado = 100 });
        await db.SaveChangesAsync();
        var service = new CreditValidationService(db);
        Assert.True(await service.PossuiEmprestimoAtivo(userId));
        Assert.False((await service.VerificarElegibilidadeRenovacao(userId)).PodeSolicitar);
    }

    [Fact]
    public async Task PartialReceivedPrincipalRestoresOnlyItsShareOfCredit()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        var loanId = Guid.NewGuid();
        db.ScoreCreditRules.Add(new ScoreCreditRule { Id = Guid.NewGuid(), Ativo = true, ScoreMin = 150, ScoreMax = 1000, LimiteEmprestimo = 200 });
        db.LoanRequests.Add(new LoanRequest { Id = loanId, UserId = userId, Status = "EmPagamento", ValorSolicitado = 80 });
        db.LoanInstallments.Add(new LoanInstallment { Id = Guid.NewGuid(), LoanRequestId = loanId, ValorPrincipal = 80, ValorJuros = 20, ValorParcela = 100, ValorPago = 40, Status = "Parcial" });
        await db.SaveChangesAsync();
        var result = await new CreditValidationService(db).ValidarCreditoDoUsuario(500, userId);
        Assert.Equal(152, result.LimiteDisponivel); // 80 principal less 32 received = 48 committed.
    }

    [Fact]
    public async Task ApprovedLoanWithoutScheduleStillConsumesCredit()
    {
        using var db = CreateContext();
        var userId = Guid.NewGuid();
        db.ScoreCreditRules.Add(new ScoreCreditRule { Id = Guid.NewGuid(), Ativo = true, ScoreMin = 150, ScoreMax = 1000, LimiteEmprestimo = 200 });
        db.LoanRequests.Add(new LoanRequest { Id = Guid.NewGuid(), UserId = userId, Status = "Financiado", ValorSolicitado = 80 });
        await db.SaveChangesAsync();
        Assert.Equal(120, (await new CreditValidationService(db).ValidarCreditoDoUsuario(500, userId)).LimiteDisponivel);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
