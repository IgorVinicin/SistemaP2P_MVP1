using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace backend.Application.Services
{
    public class DailyYieldBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyYieldBackgroundService> _logger;

        public DailyYieldBackgroundService(IServiceProvider serviceProvider, ILogger<DailyYieldBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Serviço de Rendimento Diário Automático iniciado.");

            // Aguarda 10 segundos na inicialização para a API estabilizar
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var yieldService = scope.ServiceProvider.GetRequiredService<InvestorYieldService>();

                    var (processed, totalYield) = await yieldService.ProcessDailyYieldAsync("Robo-Rendimento-Diario");
                    if (processed > 0)
                    {
                        _logger.LogInformation("Rendimento diário automático processado para {Count} investidores. Total distribuído: R$ {Total:N2}", processed, totalYield);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao executar rotina de rendimento diário automático.");
                }

                // Executa a cada 1 hora para verificar a virada de dia e creditar investidores
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
