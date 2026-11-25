using Microsoft.EntityFrameworkCore;
using YEAPI.Models;

namespace YEAPI.Services
{
    public class StopUpdateService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public StopUpdateService(
            IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;

                    // Следующий запуск в 00, 15, 30 или 45 минут
                    var nextMinute =
                        ((now.Minute / 15) + 1) * 15;

                    DateTime nextRun;

                    if (nextMinute >= 60)
                    {
                        nextRun = now.Date
                            .AddHours(now.Hour + 1);
                    }
                    else
                    {
                        nextRun = now.Date
                            .AddHours(now.Hour)
                            .AddMinutes(nextMinute);
                    }

                    await Task.Delay(
                        nextRun - now,
                        stoppingToken);

                    using var scope =
                        _scopeFactory.CreateScope();

                    var globalFunctions =
                        scope.ServiceProvider
                            .GetRequiredService<GlobalFunctionService>();

                    await globalFunctions.ProcessStopsAsync(
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Приложение остановлено
                }
                catch (Exception ex)
                {
                    using var scope =
                        _scopeFactory.CreateScope();

                    var db = scope.ServiceProvider
                        .GetRequiredService<YEContext>();

                    db.YEServicesLogs.Add(new YEServicesLog
                    {
                        ServiceName = "StopUpdateService",
                        Ending = DateTime.Now,
                        Result = $"ERROR. {ex.Message}"
                    });

                    await db.SaveChangesAsync();
                }
            }
        }
    }
}