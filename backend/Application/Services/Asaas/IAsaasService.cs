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

        Task<AsaasPaymentResponseDto> CreatePaymentAsync(GatewayCharge charge, User user, DateTime dueDate);
        Task<AsaasPaymentResponseDto?> FindPaymentAsync(string externalReference);
        Task<PixPaymentDetailsDto> GetPixDetailsAsync(string paymentId);
        Task<AsaasTransferResponseDto> CreateTransferAsync(GatewayTransfer transfer);
        Task<AsaasTransferResponseDto?> FindTransferAsync(string externalReference);
        Task CancelPaymentAsync(string paymentId);
    }
}
