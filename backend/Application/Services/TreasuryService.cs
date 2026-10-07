using System.Data;
using backend.Application.Services.Asaas;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace backend.Application.Services;

public class TreasuryService(AppDbContext context, LedgerService? ledgerService = null) : ITreasuryService
{
    private readonly LedgerService _ledger = ledgerService ?? new LedgerService(context);
    private async Task<IDbContextTransaction?> BeginAsync() => context.Database.IsRelational() && context.Database.CurrentTransaction == null
        ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;

    public async Task<DepositRequest> RequestDepositAsync(Guid investorId, decimal amount, string? observacao)
    {
        GatewayWorkflowService.ValidateMoney(amount);
        await EnsureApprovedInvestorAsync(investorId);
        if (!await context.InvestorWallets.AnyAsync(w => w.Id == investorId)) throw new ArgumentException("Carteira do investidor não encontrada.");
        var request = new DepositRequest { Id = Guid.NewGuid(), InvestorId = investorId, Valor = amount, Observacao = observacao };
        context.DepositRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }
    public async Task<bool> ConfirmDepositAsync(Guid depositId, string gatewayReference, string adminUser)
    {
        if (string.IsNullOrWhiteSpace(gatewayReference)) throw new ArgumentException("Referência do gateway obrigatória.");
        await using var tx = await BeginAsync();
        var request = await context.DepositRequests.FindAsync(depositId) ?? throw new ArgumentException("Depósito não encontrado.");
        if (request.Status == "Completed" && request.GatewayReference == gatewayReference) return true;
        if (request.Status is not ("Pending" or "Processing")) throw new InvalidOperationException("Depósito não está pendente.");
        if (request.GatewayReference != gatewayReference) throw new InvalidOperationException("Referência de pagamento não corresponde ao depósito.");
        var wallet = await context.InvestorWallets.FindAsync(request.InvestorId) ?? throw new ArgumentException("Carteira não encontrada.");
        var previous = wallet.SaldoDisponivel;
        wallet.SaldoDisponivel += request.Valor; wallet.TotalAportado += request.Valor; wallet.DataAtualizacao = DateTime.UtcNow;
        request.Status = "Completed"; request.ProcessedAt = DateTime.UtcNow;
        AddMovement(wallet, "APORTE", request.Valor, previous, $"Depósito confirmado pelo gateway: {gatewayReference}", adminUser);
        await _ledger.AddTransactionAsync(LedgerAccount.ExternalGateway, LedgerAccount.PlatformCash, request.Valor, LedgerTransactionType.InvestorDeposit,
            "DepositRequest", request.Id, userId: request.InvestorId, createdBy: adminUser);
        await _ledger.AddTransactionAsync(LedgerAccount.PlatformCash, LedgerAccount.InvestorWallet, request.Valor, LedgerTransactionType.InvestorDeposit,
            "DepositRequest", request.Id, userId: request.InvestorId, createdBy: adminUser);
        await context.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return true;
    }
    public async Task<bool> CancelDepositAsync(Guid depositId, string adminUser, string? motivo)
    {
        await using var tx = await BeginAsync();
        var request = await context.DepositRequests.FindAsync(depositId) ?? throw new ArgumentException("Depósito não encontrado.");
        if (request.Status != "Pending") throw new InvalidOperationException("Depósito com cobrança enviada deve ser cancelado no gateway antes da baixa local.");
        if (await context.GatewayCharges.AnyAsync(c => c.Kind == "Deposit" && c.ReferenceId == depositId))
            throw new InvalidOperationException("Concilie ou cancele a cobrança no Asaas antes de cancelar o depósito.");
        request.Status = "Cancelled"; request.ProcessedAt = DateTime.UtcNow; request.Observacao += $" | Cancelado por {adminUser}: {motivo}";
        await context.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return true;
    }
    public async Task<WithdrawalRequest> RequestWithdrawalAsync(Guid investorId, decimal amount, string? observacao, string? chavePix = null, string? tipoChavePix = null)
    {
        GatewayWorkflowService.ValidateMoney(amount); GatewayWorkflowService.ValidatePix(chavePix, tipoChavePix);
        await EnsureApprovedInvestorAsync(investorId);
        await using var tx = await BeginAsync();
        if (await context.GatewayWebhookEvents.AnyAsync(e => e.Status == "NeedsReview")) throw new InvalidOperationException("Saques temporariamente bloqueados até conciliação das ocorrências do gateway.");
        var wallet = await context.InvestorWallets.FindAsync(investorId) ?? throw new ArgumentException("Carteira não encontrada.");
        var availability = await new WithdrawalAvailabilityService(context).GetAsync(wallet);
        if (availability.SaldoSacavel < amount)
            throw new InvalidOperationException("Saldo sacável insuficiente. Depósitos ficam disponíveis para saque 72 horas após a confirmação.");
        var previous = wallet.SaldoDisponivel;
        wallet.SaldoDisponivel -= amount; wallet.SaldoBloqueado += amount; wallet.DataAtualizacao = DateTime.UtcNow;
        var request = new WithdrawalRequest { Id = Guid.NewGuid(), InvestorId = investorId, Valor = amount, ValorSolicitado = amount, Observacao = observacao,
            ChavePix = chavePix!.Trim(), TipoChavePix = AsaasService.NormalizePixKeyType(tipoChavePix) };
        context.WithdrawalRequests.Add(request);
        AddMovement(wallet, "SOLICITACAO_SAQUE", -amount, previous, "Saldo reservado para saque PIX", "Investidor");
        await context.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return request;
    }
    public async Task<bool> ApproveWithdrawalAsync(Guid withdrawalId, string adminUser, decimal? approvedAmount = null)
    {
        await using var tx = await BeginAsync();
        var request = await context.WithdrawalRequests.FindAsync(withdrawalId) ?? throw new ArgumentException("Saque não encontrado.");
        if (request.Status != "Pending") throw new InvalidOperationException("Saque não está pendente.");
        GatewayWorkflowService.ValidatePix(request.ChavePix, request.TipoChavePix);
        var approved = approvedAmount ?? request.Valor;
        GatewayWorkflowService.ValidateMoney(approved);
        if (approved > request.Valor) throw new InvalidOperationException("O valor aprovado não pode exceder o valor solicitado.");
        var wallet = await context.InvestorWallets.FindAsync(request.InvestorId) ?? throw new ArgumentException("Carteira não encontrada.");
        if (wallet.SaldoBloqueado < request.Valor) throw new InvalidOperationException("Reserva de saque inconsistente. Conciliação necessária.");
        var original = request.ValorSolicitado ?? request.Valor;
        var difference = request.Valor - approved;
        if (difference > 0)
        {
            var previous = wallet.SaldoDisponivel;
            wallet.SaldoBloqueado -= difference;
            wallet.SaldoDisponivel += difference;
            wallet.DataAtualizacao = DateTime.UtcNow;
            AddMovement(wallet, "ESTORNO_PARCIAL_SAQUE", difference, previous,
                $"Saque {request.Id}: solicitado {original:C2}, aprovado {approved:C2}; diferença liberada.", adminUser);
        }
        request.ValorSolicitado = original;
        request.Valor = approved;
        request.Status = "Approved";
        request.Observacao += $" | Aprovado por {adminUser}: {approved:C2} de {original:C2}; diferença devolvida {difference:C2}.";
        context.FinancialEventHistories.Add(new FinancialEventHistory {
            Id = Guid.NewGuid(), Data = DateTime.UtcNow, UsuarioResponsavel = adminUser,
            EventoExecutado = "APROVACAO_SAQUE", Valor = approved,
            Observacao = $"Saque {request.Id}, investidor {request.InvestorId}: solicitado {original:C2}; aprovado {approved:C2}; liberado {difference:C2}."
        });
        await context.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return true;
    }
    public async Task<bool> RejectWithdrawalAsync(Guid withdrawalId, string adminUser, string? motivo)
    {
        await using var tx = await BeginAsync();
        var request = await context.WithdrawalRequests.FindAsync(withdrawalId) ?? throw new ArgumentException("Saque não encontrado.");
        if (request.Status == "Rejected") return true;
        if (request.Status is not ("Pending" or "Approved" or "Failed")) throw new InvalidOperationException("Saque enviado ao gateway não pode ser rejeitado manualmente.");
        var wallet = await context.InvestorWallets.FindAsync(request.InvestorId) ?? throw new ArgumentException("Carteira não encontrada.");
        if (wallet.SaldoBloqueado < request.Valor) throw new InvalidOperationException("Reserva de saque inconsistente. Conciliação necessária.");
        var previous = wallet.SaldoDisponivel;
        wallet.SaldoBloqueado -= request.Valor; wallet.SaldoDisponivel += request.Valor; wallet.DataAtualizacao = DateTime.UtcNow;
        request.Status = "Rejected"; request.ProcessedAt = DateTime.UtcNow; request.Observacao += $" | Rejeitado por {adminUser}: {motivo}";
        AddMovement(wallet, "ESTORNO_SAQUE", request.Valor, previous, $"Reserva de saque devolvida: {motivo}", adminUser);
        await context.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return true;
    }
    public async Task<bool> ConfirmWithdrawalAsync(Guid withdrawalId, string gatewayReference, string adminUser)
    {
        await using var tx = await BeginAsync();
        var request = await context.WithdrawalRequests.FindAsync(withdrawalId) ?? throw new ArgumentException("Saque não encontrado.");
        if (request.Status == "Completed" && request.GatewayReference == gatewayReference) return true;
        if (request.Status != "Processing" || string.IsNullOrWhiteSpace(gatewayReference) || request.GatewayReference != gatewayReference)
            throw new InvalidOperationException("Somente a transferência autorizada em processamento pode liquidar o saque.");
        var wallet = await context.InvestorWallets.FindAsync(request.InvestorId) ?? throw new ArgumentException("Carteira não encontrada.");
        if (wallet.SaldoBloqueado < request.Valor) throw new InvalidOperationException("Reserva de saque inconsistente. Conciliação necessária.");
        wallet.SaldoBloqueado -= request.Valor; wallet.TotalSacado += request.Valor; wallet.DataAtualizacao = DateTime.UtcNow;
        request.Status = "Completed"; request.ProcessedAt = DateTime.UtcNow;
        AddMovement(wallet, "SAQUE", -request.Valor, wallet.SaldoDisponivel, $"Transferência PIX confirmada: {gatewayReference}", adminUser);
        await _ledger.AddTransactionAsync(LedgerAccount.InvestorWallet, LedgerAccount.ExternalGateway, request.Valor, LedgerTransactionType.InvestorWithdrawal,
            "Withdrawal", request.Id, userId: request.InvestorId, createdBy: adminUser);
        await context.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return true;
    }
    private void AddMovement(InvestorWallet wallet, string type, decimal amount, decimal previous, string description, string actor) => context.InvestorTransactions.Add(new InvestorTransaction
    {
        Id = Guid.NewGuid(), InvestorId = wallet.Id, Tipo = type, Valor = amount, SaldoAnterior = previous, SaldoPosterior = wallet.SaldoDisponivel,
        DataMovimentacao = DateTime.UtcNow, Descricao = description, UsuarioResponsavel = actor
    });
    private async Task EnsureApprovedInvestorAsync(Guid investorId)
    {
        if (!await context.Users.AnyAsync(u => u.Id == investorId && u.Role == "Investidor" && u.Status == "Aprovado"))
            throw new InvalidOperationException("Investidor ainda não aprovado para movimentações financeiras.");
    }
}
