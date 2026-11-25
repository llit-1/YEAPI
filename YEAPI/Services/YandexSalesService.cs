using Microsoft.EntityFrameworkCore;
using YEAPI.Models;

namespace YEAPI.Services
{
    public class YandexSalesService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public YandexSalesService(
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
                    // Ждём до 00:15 следующего дня
                    var now = DateTime.Now;

                    var nextRun = now.Date.AddDays(1).AddMinutes(15);

                    await Task.Delay(
                        nextRun - now,
                        stoppingToken);

                    // Создаём scope
                    using var scope = _scopeFactory.CreateScope();

                    var globalFunctions = scope.ServiceProvider
                        .GetRequiredService<GlobalFunctionService>();

                    var db = scope.ServiceProvider
                        .GetRequiredService<YEContext>();

                    // Два предыдущих дня
                    var end = DateTime.Today;
                    var begin = end.AddDays(-2);

                    // Получаем продажи из Яндекса и сохраняем
                    var errors = new List<string>();

                    var load = await globalFunctions.LoadYandexSalesAsync(
                        begin,
                        end,
                        errors,
                        stoppingToken);

                    // Записываем результат запуска
                    var result =
                        $"{(errors.Count == 0 ? "OK" : "WARN")}. " +
                        $"Загружено продаж: {load.Loaded}, " +
                        $"пропущено заказов (уже есть): {load.SkippedOrders}, " +
                        $"перезаписано заказов: {load.RewrittenOrders}";

                    if (errors.Count > 0)
                        result += ". " + string.Join("; ", errors);

                    db.YEServicesLogs.Add(new YEServicesLog
                    {
                        ServiceName = "YandexSalesService",
                        Ending = DateTime.Now,
                        Result = Truncate(result, 200)
                    });

                    await db.SaveChangesAsync(
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Приложение остановлено
                }
                catch (Exception ex)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();

                        var db = scope.ServiceProvider
                            .GetRequiredService<YEContext>();

                        db.YEServicesLogs.Add(new YEServicesLog
                        {
                            ServiceName = "YandexSalesService",
                            Ending = DateTime.Now,
                            Result = Truncate($"ERROR. {ex.Message}", 200)
                        });

                        await db.SaveChangesAsync();
                    }
                    catch
                    {
                        // Если даже запись лога не удалась
                    }
                }
            }
        }

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];
    }
}