using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using backend.Domain.Entities;

namespace backend.Application.Services
{
    public interface ITreasuryService
    {
        // Deposit flows
        Task<DepositRequest> RequestDepositAsync(Guid investorId, decimal amount, string? observacao);
        Task<bool> ConfirmDepositAsync(Guid depositId, string gatewayReference, string adminUser);
        Task<bool> CancelDepositAsync(Guid depositId, string adminUser, string? motivo);

        // Withdrawal flows
        Task<WithdrawalRequest> RequestWithdrawalAsync(Guid investorId, decimal amount, string? observacao, string? chavePix = null, string? tipoChavePix = null);
        Task<bool> ApproveWithdrawalAsync(Guid withdrawalId, string adminUser);
        Task<bool> RejectWithdrawalAsync(Guid withdrawalId, string adminUser, string? motivo);
        Task<bool> ConfirmWithdrawalAsync(Guid withdrawalId, string gatewayReference, string adminUser);
    }
}
