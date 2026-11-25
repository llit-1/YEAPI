using YEAPI.Models;

namespace YEAPI.Services
{
    // ==============================================================
    // Отправка стоп-листа в Яндекс из буфера
    //
    // Раз в секунду проверяем YandexStopSyncBuffer. Если по точке
    // 20 секунд не было изменений стопов — отправляем один импорт
    // menu_stop_list.
    // ==============================================================

    public class YandexStopSyncService : BackgroundService
    {
        private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

        private readonly YandexStopSyncBuffer _buffer;
        private readonly IServiceScopeFactory _scopeFactory;

        public YandexStopSyncService(
            YandexStopSyncBuffer buffer,
            IServiceScopeFactory scopeFactory)
        {
            _buffer = buffer;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Tick, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Приложение остановлено
                    return;
                }

                var locations = _buffer.TakeReady(Quiet);

                foreach (var locationGuid in locations)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();

                        var yandex = scope.ServiceProvider
                            .GetRequiredService<YandexEatsService>();

                        using var response = await yandex.InitiateMenuImportAsync(
                            locationGuid,
                            "menu_stop_list");
                    }
                    catch (Exception ex)
                    {
                        await LogErrorAsync(locationGuid, ex);
                    }
                }
            }
        }

        private async Task LogErrorAsync(Guid locationGuid, Exception ex)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<YEContext>();

                var result = $"ERROR {locationGuid}. {ex.Message}";

                db.YEServicesLogs.Add(new YEServicesLog
                {
                    ServiceName = "YandexStopSyncService",
                    Ending = DateTime.Now,
                    Result = result.Length > 200 ? result[..200] : result
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
