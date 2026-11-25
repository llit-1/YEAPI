using Microsoft.EntityFrameworkCore;
using System.Globalization;
using YEAPI.Models;

namespace YEAPI.Services
{
    public class RestaurantBlockLogService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public RestaurantBlockLogService(
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
                    // Ждём до начала следующего часа (**:00)
                    var now = DateTime.Now;

                    var nextRun = now.Date.AddHours(now.Hour + 1);

                    await Task.Delay(
                        nextRun - now,
                        stoppingToken);

                    // Создаём scope
                    using var scope = _scopeFactory.CreateScope();

                    var yandexEatsService = scope.ServiceProvider
                        .GetRequiredService<YandexEatsService>();

                    var db = scope.ServiceProvider
                        .GetRequiredService<YEContext>();

                    var locationTypes = new[]
                    {
                        Guid.Parse("94AD659C-AF5B-4CA0-50AD-08DBDF6ABE84"),
                        Guid.Parse("3DC24D14-FAE6-4993-A403-C4142755409A"),
                        Guid.Parse("B0E427F9-8996-4C03-33C1-08DBDF713401")
                    };

                    var locations = await db.Locations
                        .Where(x =>
                            x.LOCATIONTYPEGUID.HasValue &&
                            locationTypes.Contains(x.LOCATIONTYPEGUID.Value)
                            && x.ACTUAL == 1 && x.Delivery == 1)
                        .ToListAsync(stoppingToken);

                    var errors = new List<string>();
                    int added = 0;
                    int updated = 0;

                    foreach (var location in locations)
                    {
                        var blocks = await GetBlocksAsync(
                            yandexEatsService,
                            location,
                            errors);

                        if (blocks.Count == 0)
                            continue;

                        var blockIds = blocks
                            .Select(x => x.BlockId)
                            .ToList();

                        // Уже записанные блокировки не дублируем, а обновляем
                        var existingBlocks = await db.YERestaurantBlockLogs
                            .Where(x => blockIds.Contains(x.Id))
                            .ToDictionaryAsync(x => x.Id, stoppingToken);

                        foreach (var block in blocks)
                        {
                            var begin = ParseYandexDate(block.From);

                            if (!begin.HasValue)
                            {
                                errors.Add(
                                    $"{location.RKCODE}: block {block.BlockId} from '{block.From}'");
                                continue;
                            }

                            var end = ParseYandexDate(block.To);

                            // Initiator в таблице — nvarchar(50)
                            var initiator = block.Source?.Length > 50
                                ? block.Source[..50]
                                : block.Source;

                            if (existingBlocks.TryGetValue(block.BlockId, out var existing))
                            {
                                if (existing.Begin != begin.Value ||
                                    existing.End != end ||
                                    existing.Initiator != initiator)
                                {
                                    existing.Begin = begin.Value;
                                    existing.End = end;
                                    existing.Initiator = initiator;
                                    updated++;
                                }
                            }
                            else
                            {
                                var blockLog = new YERestaurantBlockLog
                                {
                                    Id = block.BlockId,
                                    LocationGUID = location.GUID,
                                    Initiator = initiator,
                                    Begin = begin.Value,
                                    End = end
                                };

                                db.YERestaurantBlockLogs.Add(blockLog);
                                existingBlocks[blockLog.Id] = blockLog;
                                added++;
                            }
                        }
                    }

                    await db.SaveChangesAsync(
                        stoppingToken);

                    // Записываем результат запуска
                    var result =
                        $"{(errors.Count == 0 ? "OK" : "WARN")}. " +
                        $"Точек: {locations.Count}, " +
                        $"новых блокировок: {added}, " +
                        $"обновлено: {updated}";

                    if (errors.Count > 0)
                        result += ". " + string.Join("; ", errors);

                    db.YEServicesLogs.Add(new YEServicesLog
                    {
                        ServiceName = "RestaurantBlockLogService",
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
                            ServiceName = "RestaurantBlockLogService",
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

        // Получаем блокировки точки.
        // Яндекс отдаёт их постранично — читаем, пока страница не станет неполной.
        private static async Task<List<YandexBlockStatusItem>> GetBlocksAsync(
            YandexEatsService yandexEatsService,
            Location location,
            List<string> errors)
        {
            const int limit = 100;

            var blocks = new List<YandexBlockStatusItem>();

            for (int offset = 0; ; offset += limit)
            {
                using var response =
                    await yandexEatsService.GetBlockStatusAsync(
                        location.GUID,
                        new List<string>(),
                        limit,
                        offset);

                if (!response.IsSuccessStatusCode)
                {
                    errors.Add(
                        $"{location.RKCODE}: status {(int)response.StatusCode}");
                    break;
                }

                var page = await response.Content
                    .ReadFromJsonAsync<YandexBlockStatusResponse>();

                if (page?.Items == null || page.Items.Count == 0)
                    break;

                blocks.AddRange(page.Items);

                if (page.Items.Count < limit)
                    break;
            }

            // Id в таблице — nvarchar(20)
            var tooLong = blocks.Count(x =>
                !string.IsNullOrEmpty(x.BlockId) &&
                x.BlockId.Length > 20);

            if (tooLong > 0)
                errors.Add($"{location.RKCODE}: blockId > 20 ({tooLong})");

            return blocks
                .Where(x =>
                    !string.IsNullOrEmpty(x.BlockId) &&
                    x.BlockId.Length <= 20)
                .DistinctBy(x => x.BlockId)
                .ToList();
        }

        // Яндекс отдаёт даты в виде 2026-10-09T09:00:01+0300
        private static DateTime? ParseYandexDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (DateTimeOffset.TryParseExact(
                    value,
                    "yyyy-MM-dd'T'HH:mm:sszzz",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var exact))
            {
                return exact.LocalDateTime;
            }

            if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                return parsed.LocalDateTime;
            }

            return null;
        }

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];
    }
}
