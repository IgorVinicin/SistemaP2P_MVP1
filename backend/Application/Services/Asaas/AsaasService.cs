using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using backend.Application.DTOs;
using backend.Domain.Entities;

namespace backend.Application.Services.Asaas
{
    public class AsaasService : IAsaasService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AsaasService> _logger;

        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _environment;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && !_apiKey.Contains("seu_token");
        public string EnvironmentName => _environment;
        public string BaseUrl => _baseUrl;

        public AsaasService(HttpClient httpClient, IConfiguration configuration, ILogger<AsaasService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            // Prioriza variável de ambiente (definida no .env ou host), com fallback no appsettings.json
            _apiKey = Environment.GetEnvironmentVariable("ASAAS_API_KEY") 
                      ?? _configuration["Asaas:ApiKey"] 
                      ?? string.Empty;

            _baseUrl = Environment.GetEnvironmentVariable("ASAAS_BASE_URL") 
                       ?? _configuration["Asaas:BaseUrl"] 
                       ?? "https://sandbox.asaas.com/api/v3";

            _environment = Environment.GetEnvironmentVariable("ASAAS_ENVIRONMENT") 
                           ?? _configuration["Asaas:Environment"] 
                           ?? (_baseUrl.Contains("sandbox") ? "Sandbox" : "Produção");

            // Normaliza URL base (remove barra final se houver)
            if (_baseUrl.EndsWith("/"))
            {
                _baseUrl = _baseUrl.TrimEnd('/');
            }
        }

        private void ConfigureHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SistemaP2P-Gateway/1.0");

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("access_token", _apiKey);
            }
        }

        public async Task<AsaasConnectionTestResultDto> TestConnectionAsync()
        {
            if (!IsConfigured)
            {
                return new AsaasConnectionTestResultDto
                {
                    Success = false,
                    Message = "A chave de API do Asaas (ASAAS_API_KEY) não está configurada no arquivo .env do backend.",
                    Environment = _environment,
                    BaseUrl = _baseUrl,
                    CheckedAt = DateTime.UtcNow
                };
            }

            try
            {
                ConfigureHeaders();

                // 1. Consulta saldo da conta para validar conectividade e permissão
                var balanceResponse = await _httpClient.GetAsync($"{_baseUrl}/finance/balance");
                var balanceContent = await balanceResponse.Content.ReadAsStringAsync();

                if (!balanceResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[AsaasService] Falha no teste de conexão: {Status} - {Body}", balanceResponse.StatusCode, balanceContent);
                    return new AsaasConnectionTestResultDto
                    {
                        Success = false,
                        Message = $"Erro de autenticação com o Asaas ({balanceResponse.StatusCode}): {balanceContent}",
                        Environment = _environment,
                        BaseUrl = _baseUrl,
                        CheckedAt = DateTime.UtcNow
                    };
                }

                decimal? balance = null;
                try
                {
                    using var doc = JsonDocument.Parse(balanceContent);
                    if (doc.RootElement.TryGetProperty("totalBalance", out var tb))
                    {
                        balance = tb.GetDecimal();
                    }
                    else if (doc.RootElement.TryGetProperty("balance", out var b))
                    {
                        balance = b.GetDecimal();
                    }
                }
                catch { }

                // 2. Tenta obter dados da conta
                string? accountName = null;
                string? cpfCnpj = null;
                try
                {
                    var accountResponse = await _httpClient.GetAsync($"{_baseUrl}/myAccount/status");
                    if (accountResponse.IsSuccessStatusCode)
                    {
                        var accountContent = await accountResponse.Content.ReadAsStringAsync();
                        using var accDoc = JsonDocument.Parse(accountContent);
                        if (accDoc.RootElement.TryGetProperty("commercialName", out var cn))
                            accountName = cn.GetString();
                        else if (accDoc.RootElement.TryGetProperty("companyName", out var compN))
                            accountName = compN.GetString();
                    }
                }
                catch { }

                return new AsaasConnectionTestResultDto
                {
                    Success = true,
                    Message = "Conexão com a API do Asaas estabelecida com sucesso!",
                    Environment = _environment,
                    BaseUrl = _baseUrl,
                    Balance = balance,
                    AccountName = accountName,
                    CpfCnpj = cpfCnpj,
                    CheckedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AsaasService] Exceção ao testar conexão com o Asaas");
                return new AsaasConnectionTestResultDto
                {
                    Success = false,
                    Message = $"Exceção ao conectar com a API: {ex.Message}",
                    Environment = _environment,
                    BaseUrl = _baseUrl,
                    CheckedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<string> GetOrCreateCustomerAsync(User user)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Integração Asaas não configurada. Defina ASAAS_API_KEY no arquivo .env.");
            }

            ConfigureHeaders();

            var cpf = user.Profile?.CPF?.Replace(".", "").Replace("-", "").Trim() ?? "00000000000";
            var nome = user.Profile?.Nome ?? user.Email;
            var email = user.Email;
            var telefone = user.Profile?.Telefone?.Replace("(", "").Replace(")", "").Replace("-", "").Replace(" ", "").Trim();

            // 1. Tenta buscar cliente existente pelo CPF ou Email
            try
            {
                var searchUrl = $"{_baseUrl}/customers?cpfCnpj={cpf}";
                var searchRes = await _httpClient.GetAsync(searchUrl);
                if (searchRes.IsSuccessStatusCode)
                {
                    var searchContent = await searchRes.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(searchContent);
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                    {
                        var firstCust = data[0];
                        if (firstCust.TryGetProperty("id", out var idProp))
                        {
                            var custId = idProp.GetString();
                            if (!string.IsNullOrEmpty(custId)) return custId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AsaasService] Falha ao consultar cliente por CPF: {Cpf}", cpf);
            }

            // 2. Se não encontrou, cria novo cliente
            var payload = new AsaasCustomerRequestDto
            {
                Name = nome,
                CpfCnpj = cpf,
                Email = email,
                MobilePhone = telefone,
                ExternalReference = $"USER_{user.Id}"
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var createRes = await _httpClient.PostAsync($"{_baseUrl}/customers", jsonContent);
            var createContent = await createRes.Content.ReadAsStringAsync();

            if (!createRes.IsSuccessStatusCode)
            {
                _logger.LogError("[AsaasService] Erro ao criar cliente: {Status} - {Body}", createRes.StatusCode, createContent);
                throw new Exception($"Falha ao criar cliente no Asaas: {createContent}");
            }

            var createdCustomer = JsonSerializer.Deserialize<AsaasCustomerResponseDto>(createContent);
            if (createdCustomer == null || string.IsNullOrEmpty(createdCustomer.Id))
            {
                throw new Exception("Resposta inválida do Asaas ao criar cliente.");
            }

            return createdCustomer.Id;
        }

        public async Task<PixPaymentDetailsDto> CreatePixDepositAsync(DepositRequest deposit, User user)
        {
            var customerId = await GetOrCreateCustomerAsync(user);

            var paymentRequest = new AsaasPaymentRequestDto
            {
                Customer = customerId,
                BillingType = "PIX",
                Value = deposit.Valor,
                DueDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"),
                Description = $"Aporte de Investidor - Sistema P2P (Depósito #{deposit.Id.ToString().Substring(0, 8)})",
                ExternalReference = $"DEPOSIT_{deposit.Id}"
            };

            ConfigureHeaders();
            var jsonContent = new StringContent(JsonSerializer.Serialize(paymentRequest), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync($"{_baseUrl}/payments", jsonContent);
            var content = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("[AsaasService] Erro ao criar cobrança PIX: {Status} - {Body}", res.StatusCode, content);
                throw new Exception($"Falha ao gerar cobrança PIX no Asaas: {content}");
            }

            var payment = JsonSerializer.Deserialize<AsaasPaymentResponseDto>(content);
            if (payment == null || string.IsNullOrEmpty(payment.Id))
            {
                throw new Exception("Falha ao obter dados da cobrança gerada.");
            }

            // Busca QR Code e Copia e Cola
            var qrCodeRes = await _httpClient.GetAsync($"{_baseUrl}/payments/{payment.Id}/pixQrCode");
            var qrCodeContent = await qrCodeRes.Content.ReadAsStringAsync();

            var qrCodeDto = JsonSerializer.Deserialize<AsaasPixQrCodeResponseDto>(qrCodeContent);

            return new PixPaymentDetailsDto
            {
                PaymentId = payment.Id,
                Value = payment.Value,
                QrCodeBase64 = qrCodeDto?.EncodedImage ?? string.Empty,
                CopiaECola = qrCodeDto?.Payload ?? string.Empty,
                InvoiceUrl = payment.InvoiceUrl,
                Status = payment.Status,
                ExternalReference = paymentRequest.ExternalReference
            };
        }

        public async Task<PixPaymentDetailsDto> CreatePixInstallmentPaymentAsync(LoanInstallment installment, User borrower)
        {
            var customerId = await GetOrCreateCustomerAsync(borrower);

            decimal totalDevido = installment.ValorParcela + installment.Multa + installment.JurosMora - installment.ValorPago;

            var paymentRequest = new AsaasPaymentRequestDto
            {
                Customer = customerId,
                BillingType = "PIX",
                Value = Math.Round(totalDevido, 2),
                DueDate = installment.DataVencimento > DateTime.UtcNow ? installment.DataVencimento.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Description = $"Pagamento Parcela #{installment.NumeroParcela} - Empréstimo P2P",
                ExternalReference = $"INSTALLMENT_{installment.Id}"
            };

            ConfigureHeaders();
            var jsonContent = new StringContent(JsonSerializer.Serialize(paymentRequest), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync($"{_baseUrl}/payments", jsonContent);
            var content = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("[AsaasService] Erro ao criar cobrança de parcela: {Status} - {Body}", res.StatusCode, content);
                throw new Exception($"Falha ao gerar PIX da parcela no Asaas: {content}");
            }

            var payment = JsonSerializer.Deserialize<AsaasPaymentResponseDto>(content);
            if (payment == null || string.IsNullOrEmpty(payment.Id))
            {
                throw new Exception("Falha ao obter dados da cobrança de parcela gerada.");
            }

            // Busca QR Code e Copia e Cola
            var qrCodeRes = await _httpClient.GetAsync($"{_baseUrl}/payments/{payment.Id}/pixQrCode");
            var qrCodeContent = await qrCodeRes.Content.ReadAsStringAsync();

            var qrCodeDto = JsonSerializer.Deserialize<AsaasPixQrCodeResponseDto>(qrCodeContent);

            return new PixPaymentDetailsDto
            {
                PaymentId = payment.Id,
                Value = payment.Value,
                QrCodeBase64 = qrCodeDto?.EncodedImage ?? string.Empty,
                CopiaECola = qrCodeDto?.Payload ?? string.Empty,
                InvoiceUrl = payment.InvoiceUrl,
                Status = payment.Status,
                ExternalReference = paymentRequest.ExternalReference
            };
        }

        public async Task<AsaasTransferResponseDto> CreatePixTransferAsync(WithdrawalRequest withdrawal)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Integração Asaas não configurada.");
            }

            if (string.IsNullOrWhiteSpace(withdrawal.ChavePix))
            {
                throw new ArgumentException("Chave PIX não informada para o saque.");
            }

            var transferRequest = new AsaasTransferRequestDto
            {
                Value = withdrawal.Valor,
                PixAddressKey = withdrawal.ChavePix,
                PixAddressKeyType = MapPixKeyType(withdrawal.TipoChavePix),
                Description = $"Resgate de Investimento P2P (Saque #{withdrawal.Id.ToString().Substring(0, 8)})"
            };

            ConfigureHeaders();
            var jsonContent = new StringContent(JsonSerializer.Serialize(transferRequest), Encoding.UTF8, "application/json");
            var res = await _httpClient.PostAsync($"{_baseUrl}/transfers", jsonContent);
            var content = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("[AsaasService] Erro ao solicitar transferência PIX: {Status} - {Body}", res.StatusCode, content);
                throw new Exception($"Falha ao realizar transferência PIX no Asaas: {content}");
            }

            var transfer = JsonSerializer.Deserialize<AsaasTransferResponseDto>(content);
            if (transfer == null || string.IsNullOrEmpty(transfer.Id))
            {
                throw new Exception("Falha ao obter confirmação de transferência do Asaas.");
            }

            return transfer;
        }

        private string MapPixKeyType(string? tipo)
        {
            if (string.IsNullOrWhiteSpace(tipo)) return "EVP";

            var lower = tipo.Trim().ToLower();
            if (lower.Contains("cpf")) return "CPF";
            if (lower.Contains("cnpj")) return "CNPJ";
            if (lower.Contains("email") || lower.Contains("e-mail")) return "EMAIL";
            if (lower.Contains("tel") || lower.Contains("cel") || lower.Contains("phone")) return "PHONE";
            return "EVP";
        }
    }
}
