using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Application.Services
{
    public class TreasuryService : ITreasuryService
    {
        private readonly AppDbContext _context;
        private readonly LedgerService _ledgerService;

        public TreasuryService(AppDbContext context, LedgerService ledgerService)
        {
            _context = context;
            _ledgerService = ledgerService;
        }

        public async Task<DepositRequest> RequestDepositAsync(Guid investorId, decimal amount, string? observacao)
        {
            if (amount <= 0) throw new ArgumentException("O valor do depósito deve ser maior que zero.");

            var request = new DepositRequest
            {
                Id = Guid.NewGuid(),
                InvestorId = investorId,
                Valor = amount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                Observacao = observacao
            };

            _context.DepositRequests.Add(request);
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task<bool> ConfirmDepositAsync(Guid depositId, string gatewayReference, string adminUser)
        {
            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var request = await _context.DepositRequests.FindAsync(depositId);
                    if (request == null) throw new Exception("Depósito não encontrado.");
                    if (request.Status != "Pending" && request.Status != "Processing")
                    {
                        throw new Exception($"O depósito não pode ser confirmado pois está com status: {request.Status}");
                    }

                    var wallet = await _context.InvestorWallets.FindAsync(request.InvestorId);
                    if (wallet == null) throw new Exception("Carteira do investidor não encontrada.");

                    // Atualiza status e carteira
                    request.Status = "Completed";
                    request.GatewayReference = gatewayReference;
                    request.ProcessedAt = DateTime.UtcNow;

                    decimal saldoAnterior = wallet.SaldoDisponivel;
                    wallet.SaldoDisponivel += request.Valor;
                    wallet.TotalAportado += request.Valor;
                    wallet.DataAtualizacao = DateTime.UtcNow;

                    // Registro no InvestorTransaction
                    _context.InvestorTransactions.Add(new InvestorTransaction
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = wallet.Id,
                        Tipo = "APORTE",
                        Valor = request.Valor,
                        SaldoAnterior = saldoAnterior,
                        SaldoPosterior = wallet.SaldoDisponivel,
                        DataMovimentacao = DateTime.UtcNow,
                        Descricao = $"Depósito confirmado pelo administrador. Ref Gateway: {gatewayReference}",
                        UsuarioResponsavel = adminUser
                    });

                    // Ledger: ExternalGateway -> PlatformCash -> InvestorWallet
                    await _ledgerService.AddTransactionAsync(
                        source: LedgerAccount.ExternalGateway,
                        destination: LedgerAccount.PlatformCash,
                        amount: request.Valor,
                        type: LedgerTransactionType.InvestorDeposit,
                        referenceType: "DepositRequest",
                        referenceId: request.Id,
                        userId: request.InvestorId,
                        observacao: $"Entrada de dinheiro externo na conta bancária",
                        createdBy: adminUser
                    );

                    await _ledgerService.AddTransactionAsync(
                        source: LedgerAccount.PlatformCash,
                        destination: LedgerAccount.InvestorWallet,
                        amount: request.Valor,
                        type: LedgerTransactionType.InvestorDeposit,
                        referenceType: "DepositRequest",
                        referenceId: request.Id,
                        userId: request.InvestorId,
                        observacao: $"Crédito do depósito na carteira do investidor",
                        createdBy: adminUser
                    );

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    if (attempt == maxRetries) throw new Exception("Erro de concorrência ao tentar confirmar o depósito.");
                    foreach (var entry in _context.ChangeTracker.Entries()) await entry.ReloadAsync();
                    await Task.Delay(100);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            return false;
        }

        public async Task<bool> CancelDepositAsync(Guid depositId, string adminUser, string? motivo)
        {
            var request = await _context.DepositRequests.FindAsync(depositId);
            if (request == null) throw new Exception("Depósito não encontrado.");
            if (request.Status != "Pending") throw new Exception("Apenas depósitos pendentes podem ser cancelados.");

            request.Status = "Cancelled";
            request.ProcessedAt = DateTime.UtcNow;
            request.Observacao += $" | Cancelado por: {adminUser}. Motivo: {motivo}";

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<WithdrawalRequest> RequestWithdrawalAsync(Guid investorId, decimal amount, string? observacao)
        {
            if (amount <= 0) throw new ArgumentException("O valor do saque deve ser maior que zero.");

            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var wallet = await _context.InvestorWallets.FindAsync(investorId);
                    if (wallet == null) throw new Exception("Carteira não encontrada.");

                    if (wallet.SaldoDisponivel < amount)
                    {
                        throw new Exception("Saldo insuficiente para solicitar este saque.");
                    }

                    // Move dinheiro de Disponível para Bloqueado
                    wallet.SaldoDisponivel -= amount;
                    wallet.SaldoBloqueado += amount;
                    wallet.DataAtualizacao = DateTime.UtcNow;

                    var request = new WithdrawalRequest
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = investorId,
                        Valor = amount,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow,
                        Observacao = observacao
                    };

                    _context.WithdrawalRequests.Add(request);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return request;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    if (attempt == maxRetries) throw new Exception("Concorrência intensa, tente solicitar o saque novamente.");
                    foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.Entity is InvestorWallet))
                        await entry.ReloadAsync();
                    await Task.Delay(100);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            throw new Exception("Falha inesperada ao solicitar saque.");
        }

        public async Task<bool> ApproveWithdrawalAsync(Guid withdrawalId, string adminUser)
        {
            var request = await _context.WithdrawalRequests.FindAsync(withdrawalId);
            if (request == null) throw new Exception("Saque não encontrado.");
            if (request.Status != "Pending") throw new Exception($"Saque já está em status {request.Status}.");

            request.Status = "Approved";
            request.Observacao += $" | Aprovado por {adminUser}. Aguardando gateway/processamento.";
            
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RejectWithdrawalAsync(Guid withdrawalId, string adminUser, string? motivo)
        {
            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var request = await _context.WithdrawalRequests.FindAsync(withdrawalId);
                    if (request == null) throw new Exception("Saque não encontrado.");
                    if (request.Status != "Pending" && request.Status != "Approved")
                    {
                        throw new Exception($"Não é possível rejeitar um saque no status {request.Status}.");
                    }

                    var wallet = await _context.InvestorWallets.FindAsync(request.InvestorId);
                    if (wallet == null) throw new Exception("Carteira não encontrada.");

                    // Retorna dinheiro do Bloqueado para o Disponível
                    wallet.SaldoBloqueado -= request.Valor;
                    wallet.SaldoDisponivel += request.Valor;
                    wallet.DataAtualizacao = DateTime.UtcNow;

                    request.Status = "Rejected";
                    request.ProcessedAt = DateTime.UtcNow;
                    request.Observacao += $" | Rejeitado por {adminUser}. Motivo: {motivo}";

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    if (attempt == maxRetries) throw new Exception("Erro de concorrência ao rejeitar o saque.");
                    foreach (var entry in _context.ChangeTracker.Entries()) await entry.ReloadAsync();
                    await Task.Delay(100);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            return false;
        }

        public async Task<bool> ConfirmWithdrawalAsync(Guid withdrawalId, string gatewayReference, string adminUser)
        {
            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var request = await _context.WithdrawalRequests.FindAsync(withdrawalId);
                    if (request == null) throw new Exception("Saque não encontrado.");
                    if (request.Status != "Approved" && request.Status != "Processing")
                    {
                        throw new Exception($"O saque precisa estar Aprovado para ser confirmado. Status atual: {request.Status}");
                    }

                    var wallet = await _context.InvestorWallets.FindAsync(request.InvestorId);
                    if (wallet == null) throw new Exception("Carteira não encontrada.");

                    // Remove do SaldoBloqueado definitivamente
                    wallet.SaldoBloqueado -= request.Valor;
                    wallet.TotalSacado += request.Valor;
                    wallet.DataAtualizacao = DateTime.UtcNow;

                    request.Status = "Completed";
                    request.GatewayReference = gatewayReference;
                    request.ProcessedAt = DateTime.UtcNow;

                    // Registro no InvestorTransaction
                    _context.InvestorTransactions.Add(new InvestorTransaction
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = wallet.Id,
                        Tipo = "SAQUE",
                        Valor = -request.Valor,
                        SaldoAnterior = wallet.SaldoDisponivel, // Saldo disp. não altera aqui, já alterou na solicitação
                        SaldoPosterior = wallet.SaldoDisponivel,
                        DataMovimentacao = DateTime.UtcNow,
                        Descricao = $"Saque concluído. Gateway ref: {gatewayReference}",
                        UsuarioResponsavel = adminUser
                    });

                    // Ledger: InvestorWallet -> PlatformCash -> ExternalGateway
                    await _ledgerService.AddTransactionAsync(
                        source: LedgerAccount.InvestorWallet,
                        destination: LedgerAccount.PlatformCash,
                        amount: request.Valor,
                        type: LedgerTransactionType.InvestorWithdrawal,
                        referenceType: "WithdrawalRequest",
                        referenceId: request.Id,
                        userId: request.InvestorId,
                        observacao: $"Débito do saque na carteira do investidor",
                        createdBy: adminUser
                    );

                    await _ledgerService.AddTransactionAsync(
                        source: LedgerAccount.PlatformCash,
                        destination: LedgerAccount.ExternalGateway,
                        amount: request.Valor,
                        type: LedgerTransactionType.InvestorWithdrawal,
                        referenceType: "WithdrawalRequest",
                        referenceId: request.Id,
                        userId: request.InvestorId,
                        observacao: $"Saída do dinheiro da plataforma para conta externa via Gateway",
                        createdBy: adminUser
                    );

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    if (attempt == maxRetries) throw new Exception("Erro de concorrência ao tentar confirmar o saque.");
                    foreach (var entry in _context.ChangeTracker.Entries()) await entry.ReloadAsync();
                    await Task.Delay(100);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            return false;
        }
    }
}
