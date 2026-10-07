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
using Microsoft.Extensions.Logging;

namespace backend.Tests.Services;

public class AsaasGatewayTests
{
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AsaasGatewayController Controller(AppDbContext context, FakeGateway? fake = null, string? token = "test-secret", ILogger<AsaasGatewayController>? logger = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Asaas:WebhookAccessToken"] = token }).Build();
        return new AsaasGatewayController(fake ?? new FakeGateway(), new TreasuryService(context), context, config,
            logger ?? NullLogger<AsaasGatewayController>.Instance)
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    }
    private static void Body(AsaasGatewayController controller, AsaasWebhookPayloadDto payload, string? token = "test-secret")
    {
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        controller.Request.Headers.Remove("asaas-access-token");
        if (token != null) controller.Request.Headers["asaas-access-token"] = token;
    }
    private static async Task<(User user, GatewayCharge charge)> Deposit(AppDbContext context)
    {
        var user = new User { Id = Guid.NewGuid(), Role = "Investidor", Status = "Aprovado", Email = "investor@example.com" };
        context.Users.Add(user);
        context.InvestorWallets.Add(new InvestorWallet { Id = user.Id });
        var deposit = new DepositRequest { Id = Guid.NewGuid(), InvestorId = user.Id, Valor = 100m, GatewayReference = "pay_test", Status = "Processing" };
        context.DepositRequests.Add(deposit);
        var charge = new GatewayCharge { Kind = "Deposit", ReferenceId = deposit.Id, UserId = user.Id, Value = 100m, PaymentId = "pay_test", Status = "PENDING" };
        context.GatewayCharges.Add(charge);
        await context.SaveChangesAsync();
        return (user, charge);
    }
    private static AsaasWebhookPayloadDto Received(GatewayCharge charge, string eventId = "evt_received", decimal? value = null) => new()
    {
        Id = eventId, Event = "PAYMENT_RECEIVED", Payment = new AsaasWebhookPaymentDto
        { Id = charge.PaymentId ?? "pay_test", Value = value ?? charge.Value, NetValue = (value ?? charge.Value) - 1m, Status = "RECEIVED", BillingType = "PIX", ExternalReference = $"CHARGE_{charge.Id}" }
    };

    [Fact]
    public async Task WebhookRequiresHeaderTokenAndEventIdentity()
    {
        using var context = Context();
        var (_, charge) = await Deposit(context);
        var controller = Controller(context);
        Body(controller, Received(charge), null);
        controller.Request.QueryString = new QueryString("?token=test-secret");
        Assert.IsType<UnauthorizedObjectResult>(await controller.HandleAsaasWebhook());
        var missingId = Received(charge); missingId.Id = "";
        Body(controller, missingId);
        Assert.IsType<BadRequestObjectResult>(await controller.HandleAsaasWebhook());
        Assert.Equal(0m, (await context.InvestorWallets.SingleAsync()).SaldoDisponivel);
        Assert.Empty(context.GatewayWebhookEvents);
    }
    [Fact]
    public async Task WrongValueOrPaymentIdCannotCreditWallet()
    {
        using var context = Context();
        var (_, charge) = await Deposit(context);
        var controller = Controller(context);
        Body(controller, Received(charge, value: 1m));
        Assert.IsType<BadRequestObjectResult>(await controller.HandleAsaasWebhook());
        var forged = Received(charge); forged.Payment!.Id = "pay_other";
        Body(controller, forged);
        Assert.IsType<BadRequestObjectResult>(await controller.HandleAsaasWebhook());
        Assert.Equal(0m, (await context.InvestorWallets.SingleAsync()).SaldoDisponivel);
        Assert.Empty(context.LedgerTransactions);
    }
    [Fact]
    public async Task ReceivedCreditsOnceAcrossRepeatedAndDifferentEventsAndRecordsFee()
    {
        using var context = Context();
        var (_, charge) = await Deposit(context);
        var controller = Controller(context);
        Body(controller, Received(charge));
        Assert.IsType<OkObjectResult>(await controller.HandleAsaasWebhook());
        Body(controller, Received(charge));
        Assert.IsType<OkObjectResult>(await controller.HandleAsaasWebhook());
        Body(controller, Received(charge, "evt_received_again"));
        Assert.IsType<OkObjectResult>(await controller.HandleAsaasWebhook());
        var wallet = await context.InvestorWallets.SingleAsync();
        Assert.Equal(100m, wallet.SaldoDisponivel);
        Assert.Equal(100m, wallet.TotalAportado);
        Assert.Single(context.InvestorTransactions);
        Assert.Equal(3, await context.LedgerTransactions.CountAsync());
        Assert.Equal(1m, (await context.LedgerTransactions.SingleAsync(l => l.TipoMovimentacao == LedgerTransactionType.GatewayFee)).Valor);
        Assert.Equal(2, await context.GatewayWebhookEvents.CountAsync());
    }
    [Fact]
    public async Task ConfirmedPixDoesNotCreditHeldFunds()
    {
        using var context = Context();
        var (_, charge) = await Deposit(context);
        var controller = Controller(context);
        var confirmed = Received(charge, "evt_confirmed"); confirmed.Event = "PAYMENT_CONFIRMED"; confirmed.Payment!.Status = "CONFIRMED";
        Body(controller, confirmed);
        Assert.IsType<OkObjectResult>(await controller.HandleAsaasWebhook());
        Assert.Equal(0m, (await context.InvestorWallets.SingleAsync()).SaldoDisponivel);
        Body(controller, Received(charge));
        Assert.IsType<OkObjectResult>(await controller.HandleAsaasWebhook());
        Assert.Equal(100m, (await context.InvestorWallets.SingleAsync()).SaldoDisponivel);
    }
    [Fact]
    public async Task RefundEventFreezesNewWithdrawalsForReconciliation()
    {
        using var context = Context();
        var (user, charge) = await Deposit(context);
        var controller = Controller(context);
        Body(controller, Received(charge)); await controller.HandleAsaasWebhook();
        var refund = Received(charge, "evt_refunded"); refund.Event = "PAYMENT_REFUNDED"; refund.Payment!.Status = "REFUNDED";
        Body(controller, refund);
        Assert.IsType<OkObjectResult>(await controller.HandleAsaasWebhook());
        Assert.Equal("NeedsReview", (await context.GatewayWebhookEvents.SingleAsync(e => e.EventId == refund.Id)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new TreasuryService(context).RequestWithdrawalAsync(user.Id, 10m, null, "investor@example.com", "EMAIL"));
    }
    [Fact]
    public async Task QrFailureKeepsPaymentIdentityForSafeReopen()
    {
        using var context = Context();
        var (_, charge) = await Deposit(context);
        charge.PaymentId = null; charge.Status = "Creating"; await context.SaveChangesAsync();
        var fake = new FakeGateway { FailQr = true };
        var workflow = new GatewayWorkflowService(context, fake, new TreasuryService(context));
        var user = await context.Users.SingleAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => workflow.IssueChargeAsync(charge, user, true));
        Assert.Equal("pay_test", charge.PaymentId);
        fake.FailQr = false;
        await workflow.IssueChargeAsync(charge, user);
        Assert.Equal(1, fake.CreatePaymentCalls);
        Assert.Equal("valid-pix", charge.CopiaECola);
        Assert.Equal(0m, (await context.InvestorWallets.SingleAsync()).SaldoDisponivel);
    }
    [Fact]
    public async Task UncertainTransferNeverRepeatsPost()
    {
        using var context = Context();
        var user = new User { Id = Guid.NewGuid(), Role = "Investidor", Status = "Aprovado" };
        context.Users.Add(user); context.InvestorWallets.Add(new InvestorWallet { Id = user.Id, SaldoDisponivel = 100m });
        context.DepositRequests.Add(new DepositRequest { Id = Guid.NewGuid(), InvestorId = user.Id, Valor = 100m, Status = "Completed", ProcessedAt = DateTime.UtcNow.AddHours(-100) });
        await context.SaveChangesAsync();
        var treasury = new TreasuryService(context);
        var withdrawal = await treasury.RequestWithdrawalAsync(user.Id, 40m, null, "investor@example.com", "EMAIL");
        await treasury.ApproveWithdrawalAsync(withdrawal.Id, "Admin");
        var fake = new FakeGateway { FailTransfer = true };
        var workflow = new GatewayWorkflowService(context, fake, treasury);
        await Assert.ThrowsAsync<HttpRequestException>(() => workflow.SubmitWithdrawalAsync(withdrawal.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.SubmitWithdrawalAsync(withdrawal.Id));
        Assert.Equal(1, fake.CreateTransferCalls);
        Assert.Equal("Processing", withdrawal.Status);
        Assert.Equal(40m, (await context.InvestorWallets.SingleAsync()).SaldoBloqueado);
        Assert.Empty(context.LedgerTransactions);
    }

    [Fact]
    public async Task HistoricalUnrecognizedPartialPaymentCannotCreatePix()
    {
        using var context = Context();
        var borrower = new User { Id = Guid.NewGuid(), Role = "Cliente", Status = "Aprovado" };
        context.Users.Add(borrower);
        var loan = new LoanRequest { Id = Guid.NewGuid(), UserId = borrower.Id, ValorSolicitado = 80, Status = "EmPagamento" };
        context.LoanRequests.Add(loan);
        var installment = new LoanInstallment { Id = Guid.NewGuid(), LoanRequestId = loan.Id, NumeroParcela = 1, ValorPrincipal = 80, ValorJuros = 20, ValorParcela = 100, ValorPago = 40, Status = "Parcial" };
        context.LoanInstallments.Add(installment);
        await context.SaveChangesAsync();
        var fake = new FakeGateway();
        var workflow = new GatewayWorkflowService(context, fake, new TreasuryService(context));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.CreateInstallmentAsync(installment.Id, borrower.Id, false, 10));
        Assert.Contains("conciliação histórica", error.Message);
        Assert.Equal(0, fake.CreatePaymentCalls);
        Assert.Empty(context.GatewayCharges);
        Assert.Equal(40, installment.ValorPago);
        Assert.Equal(0, installment.PrincipalPago);
    }
    [Theory]
    [InlineData("TRANSFER_DONE", "DONE", 40, 0)]
    [InlineData("TRANSFER_FAILED", "FAILED", 0, 40)]
    public async Task TransferCompletionSettlesAndFailureReleasesReservationOnce(string eventName, string remoteStatus, decimal totalWithdrawn, decimal available)
    {
        using var context = Context();
        var user = new User { Id = Guid.NewGuid(), Role = "Investidor", Status = "Aprovado" };
        context.Users.Add(user); context.InvestorWallets.Add(new InvestorWallet { Id = user.Id, SaldoBloqueado = 40m });
        var withdrawal = new WithdrawalRequest { Id = Guid.NewGuid(), InvestorId = user.Id, Valor = 40m, Status = "Processing", GatewayReference = "tr_test" };
        context.WithdrawalRequests.Add(withdrawal);
        var transfer = new GatewayTransfer { Kind = "Withdrawal", ReferenceId = withdrawal.Id, UserId = user.Id, TransferId = "tr_test", Value = 40m, Status = "PENDING" };
        context.GatewayTransfers.Add(transfer); await context.SaveChangesAsync();
        var payload = new AsaasWebhookPayloadDto { Id = "evt_transfer", Event = eventName, Transfer = new AsaasWebhookTransferDto
            { Id = "tr_test", Status = remoteStatus, Value = 40m, ExternalReference = $"TRANSFER_{transfer.Id}" } };
        var controller = Controller(context);
        Body(controller, payload); Assert.IsType<OkObjectResult>(await controller.HandleAsaasWebhook());
        payload.Id = "evt_transfer_repeat";
        Body(controller, payload); Assert.IsType<OkObjectResult>(await controller.HandleAsaasWebhook());
        var wallet = await context.InvestorWallets.SingleAsync();
        Assert.Equal(0m, wallet.SaldoBloqueado); Assert.Equal(totalWithdrawn, wallet.TotalSacado); Assert.Equal(available, wallet.SaldoDisponivel);
        Assert.Single(context.InvestorTransactions);
    }
    [Fact]
    public async Task DisbursementActivatesReservedInvestmentsOnlyAfterDone()
    {
        using var context = Context();
        var investor = new User { Id = Guid.NewGuid(), Role = "Investidor", Status = "Aprovado" };
        var borrower = new User { Id = Guid.NewGuid(), Role = "Cliente", Status = "Aprovado" };
        context.Users.AddRange(investor, borrower);
        context.UserProfiles.Add(new UserProfile { Id = borrower.Id, CPF = "52998224725", Nome = "Tomador" });
        var loan = new LoanRequest { Id = Guid.NewGuid(), UserId = borrower.Id, ValorSolicitado = 100m, Status = "Aprovado" };
        context.LoanRequests.Add(loan); context.InvestorWallets.Add(new InvestorWallet { Id = investor.Id, SaldoComprometido = 100m });
        context.LoanInstallments.Add(new LoanInstallment { Id = Guid.NewGuid(), LoanRequestId = loan.Id, NumeroParcela = 1, ValorPrincipal = 100m, ValorParcela = 100m });
        context.Investments.Add(new Investment { Id = Guid.NewGuid(), LoanId = loan.Id, InvestorId = investor.Id, ValorInvestido = 100m, SaldoPrincipalPendente = 100m });
        await context.SaveChangesAsync();
        var workflow = new GatewayWorkflowService(context, new FakeGateway(), new TreasuryService(context));
        var transfer = await workflow.SubmitLoanAsync(loan.Id, "52998224725", "CPF", 100m, true);
        Assert.Equal("DesembolsoPendente", loan.Status); Assert.Empty(context.LedgerTransactions);
        var payload = new AsaasWebhookPayloadDto { Id = "evt_disbursed", Event = "TRANSFER_DONE", Transfer = new AsaasWebhookTransferDto
            { Id = transfer.TransferId!, Status = "DONE", Value = 100m, ExternalReference = $"TRANSFER_{transfer.Id}" } };
        var controller = Controller(context); Body(controller, payload);
        Assert.IsType<OkObjectResult>(await controller.HandleAsaasWebhook());
        Assert.Equal("EmPagamento", loan.Status);
        Assert.Equal("Ativo", (await context.Investments.SingleAsync()).Status);
        var wallet = await context.InvestorWallets.SingleAsync(); Assert.Equal(0m, wallet.SaldoComprometido); Assert.Equal(100m, wallet.SaldoInvestido);
        Assert.Single(context.LedgerTransactions);
    }

    [Fact]
    public async Task PartialReceivedCancelsNextPixBeforeCarryingSeparatedDebt()
    {
        using var context = Context();
        var borrower = new User { Id = Guid.NewGuid(), Role = "Cliente", Status = "Aprovado" };
        context.Users.Add(borrower);
        var loan = new LoanRequest { Id = Guid.NewGuid(), UserId = borrower.Id, ValorSolicitado = 200m, Status = "EmPagamento" };
        context.LoanRequests.Add(loan);
        var first = new LoanInstallment { Id = Guid.NewGuid(), LoanRequestId = loan.Id, NumeroParcela = 1, ValorPrincipal = 100m, ValorJuros = 20m, ValorParcela = 120m };
        var next = new LoanInstallment { Id = Guid.NewGuid(), LoanRequestId = loan.Id, NumeroParcela = 2, ValorPrincipal = 100m, ValorJuros = 20m, ValorParcela = 120m };
        context.LoanInstallments.AddRange(first, next);
        var partial = new GatewayCharge { Kind = "Installment", ReferenceId = first.Id, UserId = borrower.Id, Value = 60m, PaymentId = "pay_test", Status = "PENDING" };
        var nextCharge = new GatewayCharge { Kind = "Installment", ReferenceId = next.Id, UserId = borrower.Id, Value = 120m, PaymentId = "pay_next", Status = "PENDING" };
        context.GatewayCharges.AddRange(partial, nextCharge); await context.SaveChangesAsync();
        var fake = new FakeGateway(); var logger = new RecordingLogger(); var controller = Controller(context, fake, logger: logger);
        Body(controller, Received(partial));
        var result = await controller.HandleAsaasWebhook();
        Assert.True(result is OkObjectResult, logger.Error?.ToString());
        Assert.Equal(1, fake.CancelPaymentCalls);
        Assert.Equal("CANCELLED", nextCharge.Status); Assert.False(nextCharge.IsActive);
        Assert.Equal(60m, first.ValorPago); Assert.Equal("Paga", first.Status);
        Assert.Equal(150m, next.ValorPrincipal); Assert.Equal(30m, next.ValorJuros); Assert.Equal(180m, next.ValorParcela);
        var rollover = await context.DebtRollovers.SingleAsync(); Assert.Equal(50m, rollover.Principal); Assert.Equal(10m, rollover.Interest);
        Body(controller, Received(partial, "evt_partial_repeat"));
        Assert.IsType<OkObjectResult>(await controller.HandleAsaasWebhook());
        Assert.Equal(1, fake.CancelPaymentCalls); Assert.Single(context.DebtRollovers);
    }

    private class RecordingLogger : ILogger<AsaasGatewayController>
    {
        public Exception? Error;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { Error = exception; }
    }
    public class FakeGateway : IAsaasService
    {
        public bool IsConfigured { get; set; } = true;
        public string EnvironmentName => "Test";
        public string BaseUrl => "https://api-sandbox.asaas.com/v3";
        public int CreatePaymentCalls, CreateTransferCalls, CancelPaymentCalls;
        public bool FailQr, FailTransfer;
        public System.Net.HttpStatusCode? TransferFailureStatus;
        public AsaasTransferResponseDto? RecoverTransfer;
        private GatewayCharge? _charge;
        public Task<AsaasPaymentResponseDto> CreatePaymentAsync(GatewayCharge charge, User user, DateTime dueDate)
        { CreatePaymentCalls++; _charge = charge; return Task.FromResult(new AsaasPaymentResponseDto { Id = "pay_test", Value = charge.Value, ExternalReference = $"CHARGE_{charge.Id}" }); }
        public Task<AsaasPaymentResponseDto?> FindPaymentAsync(string externalReference) => Task.FromResult<AsaasPaymentResponseDto?>(null);
        public Task<PixPaymentDetailsDto> GetPixDetailsAsync(string paymentId)
        { if (FailQr) throw new HttpRequestException("QR unavailable"); return Task.FromResult(new PixPaymentDetailsDto { PaymentId = paymentId, Value = _charge!.Value, CopiaECola = "valid-pix" }); }
        public Task<AsaasTransferResponseDto> CreateTransferAsync(GatewayTransfer transfer)
        { CreateTransferCalls++; if (FailTransfer) throw new HttpRequestException("Transfer unavailable", null, TransferFailureStatus); return Task.FromResult(new AsaasTransferResponseDto { Id = CreateTransferCalls == 1 ? "tr_test" : $"tr_test_{CreateTransferCalls}", Value = transfer.Value, ExternalReference = $"TRANSFER_{transfer.Id}", Status = "PENDING" }); }
        public Task<AsaasTransferResponseDto?> FindTransferAsync(string externalReference) => Task.FromResult(RecoverTransfer);
        public Task CancelPaymentAsync(string paymentId) { CancelPaymentCalls++; return Task.CompletedTask; }
        public Task<AsaasConnectionTestResultDto> TestConnectionAsync() => throw new NotImplementedException();
        public Task<string> GetOrCreateCustomerAsync(User user) => throw new NotImplementedException();
    }
}
