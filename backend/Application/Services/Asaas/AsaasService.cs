using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using backend.Application.DTOs;
using backend.Domain.Entities;

namespace backend.Application.Services.Asaas;
public class AsaasService : IAsaasService
{
    private readonly HttpClient _http;
    private readonly string _key;
    private readonly ILogger<AsaasService> _logger;
    public string BaseUrl { get; }
    public string EnvironmentName { get; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_key) && !_key.Contains("seu_token", StringComparison.OrdinalIgnoreCase);
    public AsaasService(HttpClient httpClient, IConfiguration configuration, ILogger<AsaasService> logger)
    {
        _http = httpClient; _logger = logger;
        _key = Environment.GetEnvironmentVariable("ASAAS_API_KEY") ?? configuration["Asaas:ApiKey"] ?? "";
        BaseUrl = (Environment.GetEnvironmentVariable("ASAAS_BASE_URL") ?? configuration["Asaas:BaseUrl"] ?? "https://api-sandbox.asaas.com/v3").TrimEnd('/');
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            (uri.Host != "api-sandbox.asaas.com" && uri.Host != "api.asaas.com" && uri.Host != "sandbox.asaas.com"))
            throw new InvalidOperationException("ASAAS_BASE_URL deve usar um endpoint HTTPS oficial do Asaas.");
        EnvironmentName = uri.Host.Contains("sandbox") ? "Sandbox" : "Produção";
        _http.Timeout = TimeSpan.FromSeconds(30);
    }
    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? payload = null)
    {
        if (!IsConfigured) throw new InvalidOperationException("Integração Asaas não configurada. Defina ASAAS_API_KEY.");
        using var request = new HttpRequestMessage(method, $"{BaseUrl}/{path}");
        request.Headers.Add("access_token", _key);
        request.Headers.UserAgent.ParseAdd("SistemaP2P/1.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (payload != null) request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Asaas devolveu HTTP {Status} para {Method} {Path}", (int)response.StatusCode, method, path.Split('?')[0]);
            throw new HttpRequestException("O Asaas recusou a operação. Verifique os dados e a configuração do gateway.", null, response.StatusCode);
        }
        return await JsonSerializer.DeserializeAsync<T>(await response.Content.ReadAsStreamAsync()) ?? throw new InvalidOperationException("Resposta inválida do Asaas.");
    }
    public async Task<AsaasConnectionTestResultDto> TestConnectionAsync()
    {
        var result = new AsaasConnectionTestResultDto { Environment = EnvironmentName, BaseUrl = BaseUrl };
        if (!IsConfigured) { result.Message = "Integração Asaas não configurada."; return result; }
        try
        {
            var balance = await SendAsync<JsonElement>(HttpMethod.Get, "finance/balance");
            result.Balance = balance.TryGetProperty("balance", out var value) ? value.GetDecimal() : null;
            result.Success = true; result.Message = "Conexão com Asaas validada.";
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Falha no teste de conexão Asaas"); result.Message = "Falha ao conectar com Asaas. Verifique credenciais e ambiente."; }
        return result;
    }
    public async Task<string> GetOrCreateCustomerAsync(User user)
    {
        var cpf = new string((user.Profile?.CPF ?? "").Where(char.IsDigit).ToArray());
        if (cpf.Length != 11 && cpf.Length != 14) throw new ArgumentException("CPF/CNPJ válido obrigatório para emitir cobranças.");
        var search = await SendAsync<JsonElement>(HttpMethod.Get, $"customers?cpfCnpj={Uri.EscapeDataString(cpf)}");
        if (search.TryGetProperty("data", out var customers) && customers.GetArrayLength() > 0)
            return customers[0].GetProperty("id").GetString() ?? throw new InvalidOperationException("Cliente Asaas inválido.");
        var created = await SendAsync<AsaasCustomerResponseDto>(HttpMethod.Post, "customers", new AsaasCustomerRequestDto
        {
            Name = user.Profile?.Nome ?? user.Email, CpfCnpj = cpf,
            Email = ValidationHelper.IsValidEmail(user.Email) ? user.Email.Trim() : null,
            MobilePhone = new string((user.Profile?.Telefone ?? "").Where(char.IsDigit).ToArray()), ExternalReference = $"USER_{user.Id}"
        });
        if (string.IsNullOrWhiteSpace(created.Id)) throw new InvalidOperationException("Cliente Asaas sem identificação.");
        return created.Id;
    }
    public async Task<AsaasPaymentResponseDto> CreatePaymentAsync(GatewayCharge charge, User user, DateTime dueDate)
    {
        var customer = await GetOrCreateCustomerAsync(user);
        return await SendAsync<AsaasPaymentResponseDto>(HttpMethod.Post, "payments", new
        {
            customer, billingType = "PIX", value = charge.Value, dueDate = dueDate.ToString("yyyy-MM-dd"),
            description = charge.Kind == "Deposit" ? "Aporte de investidor P2P" : "Pagamento de parcela P2P", externalReference = $"CHARGE_{charge.Id}",
            fine = new { value = 0, type = "FIXED" }, interest = new { value = 0 }
        });
    }
    public async Task<AsaasPaymentResponseDto?> FindPaymentAsync(string externalReference)
    {
        var result = await SendAsync<JsonElement>(HttpMethod.Get, $"payments?externalReference={Uri.EscapeDataString(externalReference)}&limit=100");
        var matches = result.GetProperty("data").EnumerateArray().Where(p => p.TryGetProperty("externalReference", out var r) && r.GetString() == externalReference).ToList();
        if (matches.Count > 1) throw new InvalidOperationException("Mais de uma cobrança encontrada. Necessária conciliação administrativa.");
        return matches.Count == 0 ? null : matches[0].Deserialize<AsaasPaymentResponseDto>();
    }
    public async Task<PixPaymentDetailsDto> GetPixDetailsAsync(string paymentId)
    {
        var payment = await SendAsync<AsaasPaymentResponseDto>(HttpMethod.Get, $"payments/{Uri.EscapeDataString(paymentId)}");
        var qr = await SendAsync<AsaasPixQrCodeResponseDto>(HttpMethod.Get, $"payments/{Uri.EscapeDataString(paymentId)}/pixQrCode");
        if (string.IsNullOrWhiteSpace(qr.Payload)) throw new InvalidOperationException("Gateway não retornou código PIX válido.");
        return new PixPaymentDetailsDto { PaymentId = payment.Id, Value = payment.Value, Status = payment.Status,
            ExternalReference = payment.ExternalReference ?? "", QrCodeBase64 = qr.EncodedImage, CopiaECola = qr.Payload,
            InvoiceUrl = payment.InvoiceUrl, ExpirationDate = DateTime.TryParse(qr.ExpirationDate, out var date) ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : null };
    }
    public Task<AsaasTransferResponseDto> CreateTransferAsync(GatewayTransfer transfer) => SendAsync<AsaasTransferResponseDto>(HttpMethod.Post, "transfers", new AsaasTransferRequestDto
    {
        Value = transfer.Value, PixAddressKey = transfer.PixKey, PixAddressKeyType = NormalizePixKeyType(transfer.PixKeyType),
        Description = transfer.Kind == "Withdrawal" ? "Saque investidor P2P" : "Desembolso empréstimo P2P", ExternalReference = $"TRANSFER_{transfer.Id}"
    });
    public async Task<AsaasTransferResponseDto?> FindTransferAsync(string externalReference)
    {
        // Transfer listing does not document an externalReference filter. Inspect every page explicitly.
        AsaasTransferResponseDto? match = null;
        for (int page = 0; page < 100; page++)
        {
            var result = await SendAsync<JsonElement>(HttpMethod.Get, $"transfers?limit=100&offset={page * 100}");
            foreach (var item in result.GetProperty("data").EnumerateArray())
            {
                if (!item.TryGetProperty("externalReference", out var reference) || reference.GetString() != externalReference) continue;
                if (match != null) throw new InvalidOperationException("Mais de uma transferência encontrada. Necessária conciliação administrativa.");
                match = item.Deserialize<AsaasTransferResponseDto>();
            }
            if (!result.TryGetProperty("hasMore", out var more) || !more.GetBoolean()) return match;
        }
        throw new InvalidOperationException("Histórico de transferências excede o limite de conciliação automática. Concilie a referência no Asaas.");
    }
    public static string NormalizePixKeyType(string? type) => type?.Trim().ToUpperInvariant() switch
    {
        "CPF" => "CPF", "CNPJ" => "CNPJ", "EMAIL" or "E-MAIL" => "EMAIL", "PHONE" or "TELEFONE" or "CELULAR" => "PHONE",
        "EVP" or "ALEATORIA" or "ALEATÓRIA" => "EVP", _ => throw new ArgumentException("Tipo de chave PIX inválido. Use CPF, CNPJ, EMAIL, PHONE ou EVP.")
    };
    public async Task CancelPaymentAsync(string paymentId)
    {
        var response = await SendAsync<JsonElement>(HttpMethod.Delete, $"payments/{Uri.EscapeDataString(paymentId)}");
        if (!response.TryGetProperty("deleted", out var deleted) || !deleted.GetBoolean())
            throw new InvalidOperationException("Cancelamento da cobrança não confirmado pelo Asaas.");
    }
}
