using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace backend.Application.Services
{
    public class CpfConsultationService : ICpfConsultationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CpfConsultationService> _logger;

        public CpfConsultationService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<CpfConsultationService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<CpfValidationResult> ConsultarCpfAsync(string cpf, DateTime? dataNascimento = null, string? nome = null)
        {
            var cleanCpf = new string(cpf.Where(char.IsDigit).ToArray());

            // 1. Validação estrutural de dígitos verificadores primeiro
            if (!ValidationHelper.IsValidCpf(cleanCpf))
            {
                return new CpfValidationResult
                {
                    IsValid = false,
                    Cpf = cleanCpf,
                    ErrorMessage = "O CPF informado é matematicamente inválido (dígitos verificadores incorretos)."
                };
            }

            var enabled = _configuration.GetValue<bool>("CpfConsultation:Enabled", true);
            var provider = _configuration.GetValue<string>("CpfConsultation:Provider", "HubDoDesenvolvedor");
            var token = _configuration.GetValue<string>("CpfConsultation:Token", "");
            var apiKey = _configuration.GetValue<string>("CpfConsultation:ApiKey", "");
            var activeKey = !string.IsNullOrWhiteSpace(token) ? token : apiKey;

            // Se não houver chave de API configurada, valida via algoritmo com aviso de mock
            if (!enabled || string.IsNullOrWhiteSpace(activeKey))
            {
                _logger.LogInformation("Consulta externa de CPF na Receita Federal está em modo simulado. Configure 'CpfConsultation:Token' no appsettings.json para habilitar a consulta real.");
                return new CpfValidationResult
                {
                    IsValid = true,
                    Cpf = cleanCpf,
                    SituacaoCadastral = "REGULAR (Simulação/Algoritmo Módulo 11)",
                    NomeReceita = nome,
                    DataNascimentoReceita = dataNascimento,
                    ConsultouReceitaFederal = false
                };
            }

            try
            {
                if (provider.Equals("HubDoDesenvolvedor", StringComparison.OrdinalIgnoreCase))
                {
                    return await ConsultarHubDoDesenvolvedorAsync(cleanCpf, activeKey, dataNascimento);
                }
                else if (provider.Equals("Infosimples", StringComparison.OrdinalIgnoreCase))
                {
                    return await ConsultarInfosimplesAsync(cleanCpf, activeKey, dataNascimento);
                }
                else if (provider.Equals("ReceitaWS", StringComparison.OrdinalIgnoreCase))
                {
                    return await ConsultarReceitaWSAsync(cleanCpf, activeKey);
                }

                // Default genérico
                return new CpfValidationResult
                {
                    IsValid = true,
                    Cpf = cleanCpf,
                    SituacaoCadastral = "REGULAR",
                    ConsultouReceitaFederal = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar CPF {Cpf} na API externa da Receita Federal ({Provider}).", cleanCpf, provider);
                
                // Em caso de falha de conexão na API de terceiros, se o CPF for válido pelo algoritmo, podemos permitir ou retornar erro configurável
                var allowOnApiError = _configuration.GetValue<bool>("CpfConsultation:AllowFallbackOnApiError", true);
                if (allowOnApiError)
                {
                    return new CpfValidationResult
                    {
                        IsValid = true,
                        Cpf = cleanCpf,
                        SituacaoCadastral = "REGULAR (Fallback de Conexão)",
                        ConsultouReceitaFederal = false
                    };
                }

                return new CpfValidationResult
                {
                    IsValid = false,
                    Cpf = cleanCpf,
                    ErrorMessage = "Falha ao validar CPF na Receita Federal. Tente novamente em instantes."
                };
            }
        }

        private async Task<CpfValidationResult> ConsultarHubDoDesenvolvedorAsync(string cleanCpf, string token, DateTime? dataNascimento)
        {
            var dataNascStr = dataNascimento.HasValue ? dataNascimento.Value.ToString("dd/MM/yyyy") : "";
            var url = $"https://ws.hubdodesenvolvedor.com.br/v2/cpf/?cpf={cleanCpf}&data={dataNascStr}&token={token}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return new CpfValidationResult
                {
                    IsValid = false,
                    Cpf = cleanCpf,
                    ErrorMessage = "Não foi possível validar o CPF junto à base da Receita Federal."
                };
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("status", out var statusProp) && !statusProp.GetBoolean())
            {
                var message = json.TryGetProperty("message", out var msg) ? msg.GetString() : "CPF não encontrado ou cancelado na Receita Federal.";
                return new CpfValidationResult
                {
                    IsValid = false,
                    Cpf = cleanCpf,
                    ErrorMessage = message
                };
            }

            if (json.TryGetProperty("result", out var resultObj))
            {
                var nomeReceita = resultObj.TryGetProperty("nome_da_pf", out var n) ? n.GetString() : null;
                var situacao = resultObj.TryGetProperty("situacao_cadastral", out var s) ? s.GetString() : "REGULAR";

                bool isRegular = string.Equals(situacao, "REGULAR", StringComparison.OrdinalIgnoreCase);

                return new CpfValidationResult
                {
                    IsValid = isRegular,
                    Cpf = cleanCpf,
                    NomeReceita = nomeReceita,
                    SituacaoCadastral = situacao,
                    ConsultouReceitaFederal = true,
                    ErrorMessage = isRegular ? null : $"O CPF está em situação '{situacao}' na Receita Federal e não pode ser cadastrado."
                };
            }

            return new CpfValidationResult
            {
                IsValid = true,
                Cpf = cleanCpf,
                ConsultouReceitaFederal = true
            };
        }

        private async Task<CpfValidationResult> ConsultarInfosimplesAsync(string cleanCpf, string token, DateTime? dataNascimento)
        {
            var dataNascStr = dataNascimento.HasValue ? dataNascimento.Value.ToString("yyyy-MM-dd") : "";
            var url = $"https://api.infosimples.com/api/v2/consultas/receita-federal/cpf?token={token}&cpf={cleanCpf}&birthdate={dataNascStr}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return new CpfValidationResult
                {
                    IsValid = false,
                    Cpf = cleanCpf,
                    ErrorMessage = "Não foi possível validar o CPF na API da Receita Federal."
                };
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("code", out var code) && code.GetInt32() == 200)
            {
                return new CpfValidationResult
                {
                    IsValid = true,
                    Cpf = cleanCpf,
                    SituacaoCadastral = "REGULAR",
                    ConsultouReceitaFederal = true
                };
            }

            return new CpfValidationResult
            {
                IsValid = false,
                Cpf = cleanCpf,
                ErrorMessage = "CPF inválido ou não encontrado na base de dados da Receita Federal."
            };
        }

        private async Task<CpfValidationResult> ConsultarReceitaWSAsync(string cleanCpf, string token)
        {
            var url = $"https://www.receitaws.com.br/v1/cpf/{cleanCpf}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Add("Authorization", $"Bearer {token}");
            }

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new CpfValidationResult
                {
                    IsValid = false,
                    Cpf = cleanCpf,
                    ErrorMessage = "Consulta de CPF indisponível no momento."
                };
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("status", out var status) && status.GetString()?.ToUpper() == "ERROR")
            {
                return new CpfValidationResult
                {
                    IsValid = false,
                    Cpf = cleanCpf,
                    ErrorMessage = "CPF inexistente ou cancelado na Receita Federal."
                };
            }

            return new CpfValidationResult
            {
                IsValid = true,
                Cpf = cleanCpf,
                SituacaoCadastral = "REGULAR",
                ConsultouReceitaFederal = true
            };
        }
    }
}
