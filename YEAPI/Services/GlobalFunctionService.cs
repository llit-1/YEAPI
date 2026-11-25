using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Xml.Linq;
using YEAPI.Models;
using YEAPI.RK7;

namespace YEAPI.Services
{
    public class GlobalFunctionService
    {
        private readonly YEContext _db;
        private readonly YandexEatsService _yandexEatsService;
        private readonly RK7Service _rk7Service;

        public GlobalFunctionService(
            YEContext db,
            YandexEatsService yandexEatsService,
            RK7Service rk7Service)
        {
            _db = db;
            _yandexEatsService = yandexEatsService;
            _rk7Service = rk7Service;
        }

        public async Task<List<SALEOBJECTSAGREGATORS>> GetYandexSalesAsync(
            DateTime begin,
            DateTime end,
            List<string>? statuses = null,
            List<string>? errors = null)
        {
            const int historyLimit = 1000;

            var locationTypes = new[]
            {
                Guid.Parse("94AD659C-AF5B-4CA0-50AD-08DBDF6ABE84"),
                Guid.Parse("3DC24D14-FAE6-4993-A403-C4142755409A"),
                Guid.Parse("B0E427F9-8996-4C03-33C1-08DBDF713401")
            };

            var locations = await _db.Locations
                .Where(x =>
                    x.LOCATIONTYPEGUID.HasValue &&
                    locationTypes.Contains(x.LOCATIONTYPEGUID.Value)
                    && x.ACTUAL == 1 && x.Delivery == 1)
                .ToListAsync();

            // Код отмены Яндекса → наш DELETED (справочник YECancelCodes в БД).
            // Читаем на каждый запуск, чтобы новые коды подхватывались без рестарта.
            var cancelDeletedCodes = await _db.YECancelCodes
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Code, x => x.Deleted);

            var result = new List<SALEOBJECTSAGREGATORS>();

            foreach (var location in locations)
            {
                // Берём Midserver кассы с самым поздним LastConnection
                var midserver = await _db.YECashClients
                    .Where(x => x.LocationGUID == location.GUID)
                    .OrderByDescending(x => x.LastConnection)
                    .Select(x => x.Midserver)
                    .FirstOrDefaultAsync();

                // Получаем историю заказов по точке за указанный период.
                // Яндекс отдаёт максимум 1000 заказов за запрос —
                // читаем постранично, пока страница не станет неполной.
                var historyOrders = new List<YandexOrderHistoryItem>();
                bool historyFailed = false;

                for (int offset = 0; ; offset += historyLimit)
                {
                    using var historyResponse =
                        await _yandexEatsService.GetOrdersHistoryAsync(
                            location.GUID,
                            begin,
                            end,
                            statuses,
                            historyLimit,
                            offset);

                    if (!historyResponse.IsSuccessStatusCode)
                    {
                        errors?.Add(
                            $"{location.RKCODE}: history {(int)historyResponse.StatusCode}");
                        historyFailed = true;
                        break;
                    }

                    var page = await historyResponse.Content
                        .ReadFromJsonAsync<YandexOrdersHistoryResponse>();

                    if (page?.Orders == null || page.Orders.Count == 0)
                        break;

                    historyOrders.AddRange(page.Orders);

                    if (page.Orders.Count < historyLimit)
                        break;
                }

                if (historyFailed || historyOrders.Count == 0)
                    continue;

                // Получаем все eats_id
                var eatsIds = historyOrders
                    .Select(x => x.EatsId)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct()
                    .ToList();

                // Детали Yandex принимает максимум по 100 заказов
                for (int i = 0; i < eatsIds.Count; i += 100)
                {
                    var batch = eatsIds
                        .Skip(i)
                        .Take(100)
                        .ToList();

                    using var detailsResponse =
                        await _yandexEatsService.GetOrdersDetailsAsync(batch);

                    if (!detailsResponse.IsSuccessStatusCode)
                    {
                        errors?.Add(
                            $"{location.RKCODE}: details {(int)detailsResponse.StatusCode}");
                        continue;
                    }

                    var details = await detailsResponse.Content
                        .ReadFromJsonAsync<YandexOrderDetailsResponse>();

                    if (details?.Orders == null)
                        continue;

                    if (details.NotFoundEatsIds?.Count > 0)
                    {
                        errors?.Add(
                            $"{location.RKCODE}: not found {details.NotFoundEatsIds.Count}");
                    }

                    foreach (var order in details.Orders)
                    {
                        var historyOrder = historyOrders
                            .FirstOrDefault(x => x.EatsId == order.EatsId);

                        if (historyOrder == null)
                            continue;

                        // Причина отмены: из деталей, иначе из истории
                        var cancelCode =
                            order.CancelReason?.Code ??
                            historyOrder.CancelReason?.Code;

                        // DELETED: доставлен — 0, отменён — по коду отмены
                        // (справочник YECancelCodes), иначе — 1
                        int deleted = 1;

                        if (order.Status == "DELIVERED")
                        {
                            deleted = 0;
                        }

                        else if (cancelCode != null &&
                                 cancelDeletedCodes.TryGetValue(cancelCode, out var mapped))
                        {
                            deleted = mapped;
                        }
                        else if (order.Status == "CANCELLED")
                        {
                            errors?.Add(
                                $"{order.EatsId}: unknown cancel code {cancelCode ?? "null"}");
                        }

                        if (cancelCode?.Length > 50)
                            cancelCode = cancelCode[..50];

                        // В истории Яндекса Маркет Деливери приходит как "MD",
                        // а при создании заказа — как "DC".
                        int currency = historyOrder.Service switch
                        {
                            "YE" => 1010536,
                            "MD" or "DC" => 1013070,
                            _ => 0
                        };

                        if (currency == 0)
                        {
                            errors?.Add(
                                $"{order.EatsId}: unknown service {historyOrder.Service}");
                        }

                        var createdAt = historyOrder.CreatedAt;

                        int time =
                            createdAt.Hour * 10000 +
                            createdAt.Minute * 100 +
                            createdAt.Second;

                        foreach (var item in order.Items)
                        {
                            if (!int.TryParse(item.Id, out var code))
                                continue;

                            // base_price от Яндекса — уже сумма строки (цена × количество)
                            decimal sumWithoutDiscount = item.Price;

                            result.Add(new SALEOBJECTSAGREGATORS
                            {
                                MIDSERVER = midserver,
                                CODE = code,
                                SumWithoutDiscount = sumWithoutDiscount,
                                SumWithDiscount = sumWithoutDiscount,
                                Quantity = (int)item.Quantity,
                                Date = createdAt,
                                OrderType = 1014626,
                                OrderNumber = order.EatsId,
                                CURRENCY = currency,
                                RESTAURANT = location.RKCODE,
                                DELETED = deleted,
                                CancelCode = cancelCode,
                                Hour = createdAt.Hour,
                                Time = time

                            });
                        }
                    }
                }
            }

            return result;
        }

        // Загрузка продаж Яндекса за период в SALEOBJECTSAGREGATORS.
        // Уже записанные заказы пропускаем (перезапуск сервиса и т.п.).
        // Если у заказа из ответа есть хоть одна строка с DELETED = 1 —
        // удаляем все его строки и записываем заказ заново.
        public async Task<YandexSalesLoadResult> LoadYandexSalesAsync(
            DateTime begin,
            DateTime end,
            List<string>? errors = null,
            CancellationToken cancellationToken = default)
        {
            var sales = await GetYandexSalesAsync(
                begin,
                end,
                errors: errors);

            var orderNumbers = sales
                .Select(x => x.OrderNumber)
                .Distinct()
                .ToList();

            var existingRows = await _db.SALEOBJECTSAGREGATORS
                .Where(x => orderNumbers.Contains(x.OrderNumber))
                .ToListAsync(cancellationToken);

            var rewriteOrders = existingRows
                .Where(x => x.DELETED == 1)
                .Select(x => x.OrderNumber)
                .Distinct()
                .ToList();

            var skippedOrders = existingRows
                .Select(x => x.OrderNumber)
                .Distinct()
                .Where(x => !rewriteOrders.Contains(x))
                .ToList();

            var rowsToRemove = existingRows
                .Where(x => rewriteOrders.Contains(x.OrderNumber))
                .ToList();

            sales = sales
                .Where(x => !skippedOrders.Contains(x.OrderNumber))
                .ToList();

            // Удаление старых строк и запись новых — одним SaveChanges,
            // то есть в одной транзакции
            if (sales.Count > 0 || rowsToRemove.Count > 0)
            {
                _db.SALEOBJECTSAGREGATORS.RemoveRange(rowsToRemove);
                _db.SALEOBJECTSAGREGATORS.AddRange(sales);

                await _db.SaveChangesAsync(
                    cancellationToken);
            }

            return new YandexSalesLoadResult
            {
                Loaded = sales.Count,
                LoadedOrders = sales
                    .Select(x => x.OrderNumber)
                    .Distinct()
                    .Count(),
                SkippedOrders = skippedOrders.Count,
                RewrittenOrders = rewriteOrders.Count
            };
        }

        public async Task ProcessStopsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.Now;
            int expiredDeleted = 0;
            int setCount = 0;
            int removeCount = 0;
            int removeFailed = 0;

            /*
             * 1. Удаляем просроченные стопы,
             *    которые ни на одной кассе не удалось установить.
             */
            var expiredNullStops = await _db.YEStops
                .Where(x =>
                    x.RkItemStatus == 0 &&
                    x.End < now)
                .ToListAsync(cancellationToken);

            if (expiredNullStops.Count > 0)
            {
                expiredDeleted = expiredNullStops.Count;

                _db.YEStops.RemoveRange(expiredNullStops);

                await _db.SaveChangesAsync(
                    cancellationToken);
            }

            /*
             * 2. Устанавливаем стоп.
             */
            var stopsToSet = await _db.YEStops
                .Include(x => x.YEStopPack)
                .Where(x =>
                    (x.RkItemStatus == 0 ||
                     x.RkItemStatus == 2) &&
                    x.Begin <= now &&
                    x.End > now &&
                    x.LocationGUID.HasValue &&
                    x.YEStopPack != null &&
                    x.YEStopPack.PermissionLevel == 1)
                .ToListAsync(cancellationToken);

            foreach (var stop in stopsToSet)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var status = await SetStopAsync(stop);

                stop.RkItemStatus = status;

                if (status == 1)
                    setCount++;

                await _db.SaveChangesAsync(
                    cancellationToken);
            }

            /*
             * 3. Время стопа закончилось.
             */
            var stopsToRemove = await _db.YEStops
                .Where(x =>
                    (x.RkItemStatus == 1 ||
                     x.RkItemStatus == 2) &&
                    x.End <= now &&
                    x.LocationGUID.HasValue)
                .ToListAsync(cancellationToken);

            foreach (var stop in stopsToRemove)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var success = await RemoveStopAsync(stop);

                if (success)
                {
                    _db.YEStops.Remove(stop);
                    removeCount++;
                }
                else
                {
                    stop.RkItemStatus = 2;
                    removeFailed++;
                }

                await _db.SaveChangesAsync(
                    cancellationToken);
            }

            /*
             * 4. Удаляем пустые StopPack.
             */
            var emptyPacks = await _db.YEStopPack
                .Where(x => !x.Stops.Any())
                .ToListAsync(cancellationToken);

            if (emptyPacks.Count > 0)
            {
                _db.YEStopPack.RemoveRange(emptyPacks);

                await _db.SaveChangesAsync(
                    cancellationToken);
            }

            /*
             * Логируем результат запуска.
             */
            _db.YEServicesLogs.Add(new YEServicesLog
            {
                ServiceName = "StopUpdateService",
                Ending = DateTime.Now,
                Result =
                    $"OK. Set:{setCount}/{stopsToSet.Count}, " +
                    $"Remove:{removeCount}/{stopsToRemove.Count}, " +
                    $"Failed:{removeFailed}, " +
                    $"Expired:{expiredDeleted}"
            });

            await _db.SaveChangesAsync(
                cancellationToken);
        }

        private async Task<int> SetStopAsync(
            YEStop stop)
        {
            if (!stop.LocationGUID.HasValue)
                return 0;

            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            var cashes = await _db.YECashClients
                .Where(x =>
                    x.LocationGUID == stop.LocationGUID &&
                    x.Ip != null)
                .ToListAsync();

            if (cashes.Count == 0)
                return 0;

            int successCount = 0;

            foreach (var cash in cashes)
            {
                try
                {
                    var connection = new RK7Connection
                    {
                        Ip = cash.Ip!,
                        Port = settings.RK7CashPort ?? 80,
                        User = settings.RK7Login!,
                        Password = settings.RK7Pass!
                    };

                    var response = RK7Functions.SetDishRest(
                        _rk7Service,
                        connection,
                        stop.Item,
                        true);

                    if (IsSuccess(response))
                        successCount++;
                }
                catch
                {
                    // Ошибка одной кассы.
                }
            }

            if (successCount == 0)
                return 0;

            if (successCount == cashes.Count)
                return 1;

            return 2;
        }

        private async Task<bool> RemoveStopAsync(
            YEStop stop)
        {
            if (!stop.LocationGUID.HasValue)
                return false;

            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            var cashes = await _db.YECashClients
                .Where(x =>
                    x.LocationGUID == stop.LocationGUID &&
                    x.Ip != null)
                .ToListAsync();

            if (cashes.Count == 0)
                return false;

            bool allSuccess = true;

            foreach (var cash in cashes)
            {
                try
                {
                    var connection = new RK7Connection
                    {
                        Ip = cash.Ip!,
                        Port = settings.RK7CashPort ?? 80,
                        User = settings.RK7Login!,
                        Password = settings.RK7Pass!
                    };

                    var response = RK7Functions.SetDishRest(
                        _rk7Service,
                        connection,
                        stop.Item,
                        false);

                    if (!IsSuccess(response))
                        allSuccess = false;
                }
                catch
                {
                    allSuccess = false;
                }
            }

            return allSuccess;
        }

        private static bool IsSuccess(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                return false;

            try
            {
                var document = XDocument.Parse(xml);

                var commandResult = document.Root?
                    .Element("CommandResult");

                if (commandResult == null)
                    return false;

                return string.Equals(
                    commandResult.Attribute("Status")?.Value,
                    "Ok",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}