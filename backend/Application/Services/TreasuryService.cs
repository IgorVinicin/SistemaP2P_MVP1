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

        public TreasuryService(AppDbContext context, LedgerService? ledgerService = null)
        {
            _context = context;
            _ledgerService = ledgerService ?? new LedgerService(context);
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

        public async Task<WithdrawalRequest> RequestWithdrawalAsync(Guid investorId, decimal amount, string? observacao, string? chavePix = null, string? tipoChavePix = null)
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
                        throw new Exception("Saldo disponível insuficiente para solicitar este saque.");
                    }

                    decimal saldoAnt = wallet.SaldoDisponivel;

                    // Reserva/bloqueia o valor imediatamente do saldo disponível
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
                        Observacao = observacao,
                        ChavePix = chavePix,
                        TipoChavePix = tipoChavePix
                    };

                    _context.WithdrawalRequests.Add(request);

                    _context.InvestorTransactions.Add(new InvestorTransaction
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = wallet.Id,
                        Tipo = "SOLICITACAO_SAQUE",
                        Valor = -amount,
                        SaldoAnterior = saldoAnt,
                        SaldoPosterior = wallet.SaldoDisponivel,
                        DataMovimentacao = DateTime.UtcNow,
                        Descricao = $"Solicitação de saque PIX no valor de {amount:C2} (Saldo reservado para análise)",
                        UsuarioResponsavel = "Investidor"
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return request;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    if (attempt == maxRetries) throw new Exception("Erro de concorrência ao processar solicitação de saque.");
                    foreach (var entry in _context.ChangeTracker.Entries()) await entry.ReloadAsync();
                    await Task.Delay(100);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            throw new Exception("Falha ao registrar solicitação de saque.");
        }

        public async Task<bool> ApproveWithdrawalAsync(Guid withdrawalId, string adminUser)
        {
            var request = await _context.WithdrawalRequests.FindAsync(withdrawalId);
            if (request == null) throw new Exception("Saque não encontrado.");
            if (request.Status != "Pending") throw new Exception($"Saque não está pendente de análise. Status atual: {request.Status}.");

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

                    if (request.Status == "Completed")
                    {
                        throw new Exception("Não é possível recusar um saque já concluído e liquidado.");
                    }
                    if (request.Status == "Rejected" || request.Status == "Cancelled")
                    {
                        throw new Exception($"Este saque já se encontra com status {request.Status}.");
                    }
                    if (request.Status != "Pending" && request.Status != "Approved")
                    {
                        throw new Exception($"Não é possível rejeitar um saque no status {request.Status}.");
                    }

                    var wallet = await _context.InvestorWallets.FindAsync(request.InvestorId);
                    if (wallet == null) throw new Exception("Carteira não encontrada.");

                    decimal saldoAnt = wallet.SaldoDisponivel;

                    // Estorna o valor reservado/bloqueado de volta para o saldo disponível
                    if (wallet.SaldoBloqueado >= request.Valor)
                    {
                        wallet.SaldoBloqueado -= request.Valor;
                    }
                    else
                    {
                        wallet.SaldoBloqueado = 0;
                    }

                    wallet.SaldoDisponivel += request.Valor;
                    wallet.DataAtualizacao = DateTime.UtcNow;

                    request.Status = "Rejected";
                    request.ProcessedAt = DateTime.UtcNow;
                    request.Observacao += $" | Rejeitado por {adminUser}. Motivo: {motivo}";

                    _context.InvestorTransactions.Add(new InvestorTransaction
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = wallet.Id,
                        Tipo = "ESTORNO_SAQUE",
                        Valor = request.Valor,
                        SaldoAnterior = saldoAnt,
                        SaldoPosterior = wallet.SaldoDisponivel,
                        DataMovimentacao = DateTime.UtcNow,
                        Descricao = $"Estorno de saque recusado pelo administrador: {motivo}",
                        UsuarioResponsavel = adminUser
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    if (attempt == maxRetries) throw new Exception("Erro de concorrência ao recusar o saque.");
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

                    if (request.Status == "Completed")
                    {
                        throw new Exception("Este saque já foi concluído anteriormente.");
                    }
                    if (request.Status == "Rejected" || request.Status == "Cancelled")
                    {
                        throw new Exception($"Não é possível confirmar um saque com status {request.Status}.");
                    }
                    if (request.Status != "Pending" && request.Status != "Approved")
                    {
                        throw new Exception($"Não é possível confirmar um saque no status {request.Status}.");
                    }

                    var wallet = await _context.InvestorWallets.FindAsync(request.InvestorId);
                    if (wallet == null) throw new Exception("Carteira não encontrada.");

                    // Baixa definitiva do Saldo Bloqueado
                    if (wallet.SaldoBloqueado >= request.Valor)
                    {
                        wallet.SaldoBloqueado -= request.Valor;
                    }
                    else
                    {
                        decimal diff = request.Valor - wallet.SaldoBloqueado;
                        wallet.SaldoBloqueado = 0;
                        if (wallet.SaldoDisponivel >= diff)
                        {
                            wallet.SaldoDisponivel -= diff;
                        }
                        else
                        {
                            throw new Exception("Saldo insuficiente na carteira para liquidar o saque.");
                        }
                    }

                    wallet.TotalSacado += request.Valor;
                    wallet.DataAtualizacao = DateTime.UtcNow;

                    request.Status = "Completed";
                    request.ProcessedAt = DateTime.UtcNow;
                    request.GatewayReference = gatewayReference;
                    request.Observacao += $" | Confirmado e liquidado por {adminUser}. Ref: {gatewayReference}";

                    _context.InvestorTransactions.Add(new InvestorTransaction
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = wallet.Id,
                        Tipo = "SAQUE",
                        Valor = -request.Valor,
                        SaldoAnterior = wallet.SaldoDisponivel,
                        SaldoPosterior = wallet.SaldoDisponivel,
                        DataMovimentacao = DateTime.UtcNow,
                        Descricao = $"Saque PIX liquidado e transferido. Gateway ref: {gatewayReference}",
                        UsuarioResponsavel = adminUser
                    });

                    var ledgerService = new LedgerService(_context);
                    await ledgerService.AddTransactionAsync(
                        source: LedgerAccount.InvestorWallet,
                        destination: LedgerAccount.ExternalGateway,
                        amount: request.Valor,
                        type: LedgerTransactionType.InvestorWithdrawal,
                        referenceType: "Withdrawal",
                        referenceId: request.Id,
                        userId: request.InvestorId,
                        observacao: $"Saque PIX liquidado para investidor - Ref: {gatewayReference}",
                        createdBy: adminUser
                    );

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    if (attempt == maxRetries) throw new Exception("Erro de concorrência ao confirmar o saque.");
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
