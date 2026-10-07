using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using backend.API.Controllers;
using backend.Application.DTOs;
using backend.Application.Services;
using backend.Application.Services.Asaas;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Services;

public class LoanDisbursementTests
{
    private const string Cpf = "52998224725";
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<LoanRequest> Seed(AppDbContext context, string status = "Aprovado")
    {
        var borrower = new User { Id = Guid.NewGuid(), Role = "Cliente", Status = "Aprovado", Email = "borrower@example.com" };
        context.Users.Add(borrower); context.UserProfiles.Add(new UserProfile { Id = borrower.Id, CPF = Cpf, Nome = "Tomador" });
        var loan = new LoanRequest { Id = Guid.NewGuid(), UserId = borrower.Id, ValorSolicitado = 100m, ValorTotal = 110m, QuantidadeParcelas = 1, Status = status };
        context.LoanRequests.Add(loan);
        context.LoanInstallments.Add(new LoanInstallment { Id = Guid.NewGuid(), LoanRequestId = loan.Id, NumeroParcela = 1, ValorPrincipal = 100m, ValorJuros = 10m, ValorParcela = 110m, DataVencimento = DateTime.UtcNow.AddMonths(1) });
        await context.SaveChangesAsync(); return loan;
    }
    private static GatewayWorkflowService Workflow(AppDbContext context, AsaasGatewayTests.FakeGateway gateway) => new(context, gateway, new TreasuryService(context));
    private static AsaasGatewayController Controller(AppDbContext context, AsaasGatewayTests.FakeGateway gateway)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Asaas:WebhookAccessToken"] = "test-secret" }).Build();
        return new AsaasGatewayController(gateway, new TreasuryService(context), context, config, NullLogger<AsaasGatewayController>.Instance)
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    }
    private static async Task<IActionResult> Event(AppDbContext context, AsaasGatewayTests.FakeGateway gateway, GatewayTransfer transfer, string eventId, string eventName = "TRANSFER_DONE", decimal? value = null)
    {
        var controller = Controller(context, gateway);
        controller.Request.Headers["asaas-access-token"] = "test-secret";
        var payload = new AsaasWebhookPayloadDto { Id = eventId, Event = eventName, Transfer = new AsaasWebhookTransferDto
            { Id = transfer.TransferId!, Value = value ?? transfer.Value, Status = eventName == "TRANSFER_DONE" ? "DONE" : "FAILED", ExternalReference = $"TRANSFER_{transfer.Id}" } };
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        return await controller.HandleAsaasWebhook();
    }

    [Theory]
    [InlineData("11144477735", "CPF", 100, true)]
    [InlineData("borrower@example.com", "EMAIL", 100, true)]
    [InlineData(Cpf, "CPF", 99, true)]
    [InlineData(Cpf, "CPF", 100, false)]
    public async Task InvalidDestinationOrUnconfirmedQuoteCannotSend(string key, string type, decimal value, bool confirmed)
    {
        using var context = Context(); var loan = await Seed(context); var gateway = new AsaasGatewayTests.FakeGateway();
        await Assert.ThrowsAsync<ArgumentException>(() => Workflow(context, gateway).SubmitLoanAsync(loan.Id, key, type, value, confirmed));
        Assert.Equal(0, gateway.CreateTransferCalls); Assert.Empty(context.GatewayTransfers); Assert.Empty(context.LedgerTransactions);
        Assert.Equal("Aprovado", loan.Status);
    }
    [Theory]
    [InlineData("Pendente")]
    [InlineData("Reprovado")]
    [InlineData("Quitado")]
    [InlineData("EmPagamento")]
    public async Task UnauthorizedLoanStatesCannotSend(string state)
    {
        using var context = Context(); var loan = await Seed(context, state); var gateway = new AsaasGatewayTests.FakeGateway();
        await Assert.ThrowsAsync<ArgumentException>(() => Workflow(context, gateway).SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true));
        Assert.Equal(0, gateway.CreateTransferCalls); Assert.Empty(context.GatewayTransfers);
    }
    [Fact]
    public async Task UnapprovedBorrowerCannotSendOrIssuePaymentPix()
    {
        using var context = Context(); var loan = await Seed(context); var gateway = new AsaasGatewayTests.FakeGateway();
        loan.User!.Status = "Pendente"; await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Workflow(context, gateway).SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true));
        loan.Status = "EmPagamento"; await context.SaveChangesAsync();
        var installment = await context.LoanInstallments.SingleAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Workflow(context, gateway).CreateInstallmentAsync(installment.Id, loan.UserId, false, null));
        Assert.Equal(0, gateway.CreateTransferCalls); Assert.Equal(0, gateway.CreatePaymentCalls); Assert.Empty(context.GatewayCharges);
    }
    [Fact]
    public async Task IncompleteFundingCannotTransferOrActivatePositions()
    {
        using var context = Context(); var loan = await Seed(context); var gateway = new AsaasGatewayTests.FakeGateway();
        var investor = new User { Id = Guid.NewGuid(), Role = "Investidor", Status = "Aprovado" }; context.Users.Add(investor);
        context.InvestorWallets.Add(new InvestorWallet { Id = investor.Id, SaldoComprometido = 50m });
        context.Investments.Add(new Investment { Id = Guid.NewGuid(), LoanId = loan.Id, InvestorId = investor.Id, ValorInvestido = 50m, SaldoPrincipalPendente = 50m }); await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Workflow(context, gateway).SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true));
        Assert.Equal(0, gateway.CreateTransferCalls); Assert.Equal(50m, (await context.InvestorWallets.SingleAsync()).SaldoComprometido);
    }
    [Fact]
    public async Task RepeatedSubmissionAndWebhookDoNotTransferOrBookTwice()
    {
        using var context = Context(); var loan = await Seed(context, "Financiado"); var gateway = new AsaasGatewayTests.FakeGateway(); var workflow = Workflow(context, gateway);
        var transfer = await workflow.SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true, "AdminTest");
        Assert.Equal("DesembolsoPendente", loan.Status); Assert.Empty(context.LedgerTransactions);
        var same = await workflow.SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true);
        Assert.Equal(transfer.Id, same.Id); Assert.Equal(1, gateway.CreateTransferCalls);
        Assert.Single(context.FinancialEventHistories.Where(e => e.EventoExecutado == "ENVIO_DESEMBOLSO_AUTORIZADO"));
        Assert.IsType<OkObjectResult>(await Event(context, gateway, transfer, "evt_done"));
        Assert.IsType<OkObjectResult>(await Event(context, gateway, transfer, "evt_done_again"));
        Assert.Equal("EmPagamento", loan.Status); Assert.Single(context.LedgerTransactions);
        Assert.Equal(100m, (await context.LedgerTransactions.SingleAsync()).Valor);
        Assert.Equal("DONE", (await workflow.SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true)).Status);
        Assert.Equal(1, gateway.CreateTransferCalls);
    }
    [Fact]
    public async Task UncertainNetworkReconcilesOriginalAuthorizationWithoutRepeatingPost()
    {
        using var context = Context(); var loan = await Seed(context); var gateway = new AsaasGatewayTests.FakeGateway { FailTransfer = true }; var workflow = Workflow(context, gateway);
        await Assert.ThrowsAsync<HttpRequestException>(() => workflow.SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true));
        var marker = await context.GatewayTransfers.SingleAsync();
        Assert.Equal("Creating", marker.Status); Assert.Equal("DesembolsoPendente", loan.Status);
        gateway.RecoverTransfer = new AsaasTransferResponseDto { Id = "tr_recovered", Value = 100m, ExternalReference = $"TRANSFER_{marker.Id}" };
        var recovered = await workflow.SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true);
        Assert.Equal("tr_recovered", recovered.TransferId); Assert.Equal(1, gateway.CreateTransferCalls); Assert.Empty(context.LedgerTransactions);
    }
    [Fact]
    public async Task DefinitiveRejectionAllowsExplicitNewAttemptButLateDoneFreezesConflict()
    {
        using var context = Context(); var loan = await Seed(context); var gateway = new AsaasGatewayTests.FakeGateway { FailTransfer = true, TransferFailureStatus = HttpStatusCode.BadRequest }; var workflow = Workflow(context, gateway);
        await Assert.ThrowsAsync<HttpRequestException>(() => workflow.SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true));
        var failed = await context.GatewayTransfers.SingleAsync(); Assert.Equal("FAILED", failed.Status); Assert.False(failed.IsActive); Assert.Equal("FalhaDesembolso", loan.Status);
        gateway.FailTransfer = false;
        var retry = await workflow.SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true);
        Assert.NotEqual(failed.Id, retry.Id); Assert.Equal(2, gateway.CreateTransferCalls); Assert.Equal("DesembolsoPendente", loan.Status);
        failed.TransferId = "tr_late"; await context.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await Event(context, gateway, failed, "evt_late_done"));
        Assert.Equal("NeedsReview", (await context.GatewayWebhookEvents.SingleAsync()).Status); Assert.Empty(context.LedgerTransactions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true));
        Assert.Equal(2, gateway.CreateTransferCalls);
    }
    [Fact]
    public async Task MismatchedCallbackCannotBookTransfer()
    {
        using var context = Context(); var loan = await Seed(context); var gateway = new AsaasGatewayTests.FakeGateway();
        var transfer = await Workflow(context, gateway).SubmitLoanAsync(loan.Id, Cpf, "CPF", 100m, true);
        Assert.IsType<BadRequestObjectResult>(await Event(context, gateway, transfer, "evt_wrong_value", value: 1m));
        Assert.Equal("DesembolsoPendente", loan.Status); Assert.Empty(context.LedgerTransactions); Assert.Empty(context.GatewayWebhookEvents);
    }
    [Fact]
    public async Task ActiveListingsIncludePendingAndFailedDisbursementsWithNormalizedPagination()
    {
        using var context = Context();
        foreach (var state in new[] { "Aprovado", "Financiado", "DesembolsoPendente", "FalhaDesembolso" }) await Seed(context, state);
        var controller = new AdminLoanController(context, new AllocationEngineService(context));
        var response = Assert.IsType<OkObjectResult>(await controller.GetActiveLoans(page: 0, pageSize: 0));
        var body = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal(4, body.GetProperty("TotalCount").GetInt32()); Assert.Equal(1, body.GetProperty("TotalPages").GetInt32()); Assert.Equal(20, body.GetProperty("PageSize").GetInt32());
        var grouped = Assert.IsType<OkObjectResult>(await controller.GetGroupedActiveLoans(page: 0, pageSize: 0));
        Assert.Equal(4, JsonSerializer.SerializeToElement(grouped.Value).GetProperty("TotalCount").GetInt32());
        var pending = Assert.IsType<OkObjectResult>(await controller.GetPendingLoans(page: 0, pageSize: 0));
        Assert.Equal(0, JsonSerializer.SerializeToElement(pending.Value).GetProperty("TotalPages").GetInt32());
    }
}
