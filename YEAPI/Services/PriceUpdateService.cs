using YEAPI.Models;
using YEAPI.RK7;
using YEAPI.Services;
namespace YEAPI.Services
{
    public class PriceUpdateService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RK7Service _rk7Service;

        public PriceUpdateService(
            IServiceScopeFactory scopeFactory,
            RK7Service rk7Service)
        {
            _scopeFactory = scopeFactory;
            _rk7Service = rk7Service;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    var nextRun = now.Date.AddDays(1).AddMinutes(15);                 
                    await Task.Delay(nextRun - now, stoppingToken);
                    using var scope = _scopeFactory.CreateScope();

                    var db = scope.ServiceProvider
                        .GetRequiredService<YEContext>();

                    YESettings settings = db.YESettings.FirstOrDefault();

                    if (settings == null)
                        throw new Exception("Ошибка в settings");

                    var connection = new RK7Connection
                    {
                        Ip = settings.RK7Server!,
                        Port = settings.RK7ServerPort ?? 3355,
                        User = settings.RK7Login!,
                        Password = settings.RK7Pass!
                    };

                    var menu = RK7Functions.GetMenu(
                        _rk7Service,
                        connection);

                    foreach (var category in menu.Categories)
                        UpdateCategory(category, db);

                    await db.SaveChangesAsync(stoppingToken);

                    db.YEServicesLogs.Add(new YEServicesLog
                    {
                        ServiceName = "PriceUpdateService",
                        Ending = DateTime.Now,
                        Result = "OK"
                    });

                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // приложение остановлено
                }
                catch (Exception)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();

                        var db = scope.ServiceProvider
                            .GetRequiredService<YEContext>();

                        db.YEServicesLogs.Add(new YEServicesLog
                        {
                            ServiceName = "PriceUpdateService",
                            Ending = DateTime.Now,
                            Result = "ERROR"
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

        private static void UpdateCategory(
            RK7Category category,
            YEContext db)
        {
            foreach (var item in category.Items)
            {
                var dbItem = db.YEItems.Find(item.Code);

                if (dbItem != null)
                    dbItem.Price = item.Price/100;
            }

            foreach (var childCategory in category.Categories)
                UpdateCategory(childCategory, db);
        }
    }
}
