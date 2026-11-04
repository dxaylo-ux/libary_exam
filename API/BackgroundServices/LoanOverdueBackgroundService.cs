using Application.Services;
using Application.Settings;
using Microsoft.Extensions.Options;

namespace API.BackgroundServices;

public class LoanOverdueBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<LoanSettings> settings,
    ILogger<LoanOverdueBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<LoanOverdueService>();
                var count = await service.MarkOverdueAsync(stoppingToken);

                if (count > 0)
                    logger.LogInformation("Просроченных выдач помечено: {Count}", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при проверке просроченных выдач");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.Value.OverdueCheckSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
