using System;
using System.Threading.Tasks;
using backend.Application.DTOs;
using backend.Domain.Entities;

namespace backend.Application.Services.Asaas
{
    public interface IAsaasService
    {
        bool IsConfigured { get; }
        string EnvironmentName { get; }
        string BaseUrl { get; }

        Task<AsaasConnectionTestResultDto> TestConnectionAsync();

        Task<string> GetOrCreateCustomerAsync(User user);

        Task<PixPaymentDetailsDto> CreatePixDepositAsync(DepositRequest deposit, User user);

        Task<PixPaymentDetailsDto> CreatePixInstallmentPaymentAsync(LoanInstallment installment, User borrower);

        Task<AsaasTransferResponseDto> CreatePixTransferAsync(WithdrawalRequest withdrawal);
    }
}
