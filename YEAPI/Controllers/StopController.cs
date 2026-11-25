using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YEAPI.Models;
using YEAPI.Services;

namespace YEAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StopController : ControllerBase
    {
        private readonly YEContext _db;
        private readonly YandexEatsService _yandexEatsService;
        private readonly CashSignalRService _cashSignalRService;
        private readonly GlobalFunctionService _globalFunctionService;
        private readonly YandexStopSyncBuffer _stopSyncBuffer;
        public StopController(YEContext db, YandexEatsService yandexEatsService, CashSignalRService cashSignalRService, GlobalFunctionService globalFunctionService, YandexStopSyncBuffer stopSyncBuffer)
        {
            _db = db;
            _yandexEatsService = yandexEatsService;
            _cashSignalRService = cashSignalRService;
            _globalFunctionService = globalFunctionService;
            _stopSyncBuffer = stopSyncBuffer;
        }

        // GET api/Stop
        // GET api/Stop?location={guid}
        [HttpGet]
        public async Task<ActionResult<List<YEStopPack>>> Get([FromQuery] Guid? location = null)
        {
            var now = DateTime.Now;
            // Возвращаем только актуальные пакеты: с незавершёнными стопами.
            // Если передана точка — только её стопы, стопы других точек из пакетов убираем.
            var result = await _db.YEStopPack
                .Where(x => x.Stops.Any(s =>
                    s.End >= now &&
                    (location == null || s.LocationGUID == location)))
                .Include(x => x.Stops.Where(s =>
                    s.End >= now &&
                    (location == null || s.LocationGUID == location)))
                .ToListAsync();
            return Ok(result);
        }

        // GET api/Stop/logs?location={guid}&from=2026-11-01&to=2026-11-25
        // Журнал постановки и снятия стопов по точке, новые записи первыми.
        // from и to — даты создания записи включительно; по умолчанию последние 7 дней.
        [HttpGet("logs")]
        public async Task<ActionResult<List<StopLogDto>>> GetLogs(
            [FromQuery] Guid location,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            const int maxRows = 1000;
            if (location == Guid.Empty)
                return BadRequest(new { Message = "Не указана торговая точка." });

            var toDate = (to ?? DateTime.Today).Date;
            var fromDate = (from ?? toDate.AddDays(-6)).Date;
            if (fromDate > toDate)
                return BadRequest(new { Message = "Начало периода позже его окончания." });

            var toExclusive = toDate.AddDays(1);
            var logs = await (
                from log in _db.YEStoplogs.AsNoTracking()
                where log.LocationGUID == location
                    && log.Created >= fromDate
                    && log.Created < toExclusive
                join item in _db.YEItems.AsNoTracking() on log.Item equals item.Rkcode into items
                from item in items.DefaultIfEmpty()
                orderby log.Created descending, log.ID descending
                select new StopLogDto(
                    log.ID,
                    log.LocationGUID,
                    log.Item,
                    item != null ? item.RkName : null,
                    log.Begin,
                    log.End,
                    log.PermissionLevel,
                    log.User,
                    log.Created,
                    log.Type))
                .Take(maxRows)
                .ToListAsync();

            return Ok(logs);
        }

        public sealed record StopLogDto(
            int Id,
            Guid? Location,
            int Item,
            string? ItemName,
            DateTime Begin,
            DateTime End,
            int PermissionLevel,
            string User,
            DateTime Created,
            string Type);

        // POST api/Stop/
        [HttpPost]
        public async Task<ActionResult> Create(YEStopPack stopPack)
        {
            _db.YEStopPack.Add(stopPack);
            foreach (var stop in stopPack.Stops)
            {
                stop.RkItemStatus ??= 0;

                _db.YEStoplogs.Add(new YEStoplog
                {
                    LocationGUID = stop.LocationGUID,
                    Item = stop.Item,
                    Begin = stop.Begin,
                    End = stop.End,
                    User = stopPack.User,
                    Created = DateTime.Now,
                    PermissionLevel = stopPack.PermissionLevel,
                    Type = "Create"
                });
            }
            await _db.SaveChangesAsync();
            await UpdateYandexStopListAsync(stopPack);
            await PushUpdateStopSignal(stopPack);

            // Кассы (RK7) — только для глобальных стопов
            if (stopPack.PermissionLevel == 1)
                await _globalFunctionService.ProcessStopsAsync();

            return Ok();
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, YEStopPack model)
        {
            var pack = await _db.YEStopPack
                .Include(x => x.Stops)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (pack == null)
                return NotFound();

            var now = DateTime.Now;

            // Обновляем сам пакет
            pack.LocationGUID = model.LocationGUID;
            pack.ItemId = model.ItemId;
            pack.ItemName = model.ItemName;
            pack.LocationName = model.LocationName;
            pack.Begin = model.Begin;
            pack.End = model.End;
            pack.User = model.User;

            // ---------------------------------------------------------
            // 1. Старые стопы:
            //    - если стоп есть в новой модели -> меняем даты
            //    - если стопа больше нет -> завершаем его сейчас
            // ---------------------------------------------------------

            foreach (var oldStop in pack.Stops)
            {
                var newStop = model.Stops.FirstOrDefault(x =>
                    x.Item == oldStop.Item &&
                    x.LocationGUID == oldStop.LocationGUID);

                if (newStop != null)
                {
                    // Стоп продолжает существовать.
                    // Просто меняем его даты.
                    oldStop.Begin = newStop.Begin;
                    oldStop.End = newStop.End;
                }
                else
                {
                    // Стоп убрали из новой модели.
                    // НЕ удаляем его.
                    oldStop.End = now;

                    _db.YEStoplogs.Add(new YEStoplog
                    {
                        LocationGUID = oldStop.LocationGUID,
                        Item = oldStop.Item,
                        Begin = oldStop.Begin,
                        End = oldStop.End,
                        User = model.User,
                        PermissionLevel = pack.PermissionLevel,
                        Created = now,
                        Type = "Delete"
                    });
                }
            }

            // ---------------------------------------------------------
            // 2. Новые стопы:
            //    которых раньше в пакете не было
            // ---------------------------------------------------------

            foreach (var newStop in model.Stops)
            {
                var existingStop = pack.Stops.FirstOrDefault(x =>
                    x.Item == newStop.Item &&
                    x.LocationGUID == newStop.LocationGUID);

                if (existingStop != null)
                    continue;

                // Это новый стоп
                var stop = new YEStop
                {
                    PackId = pack.Id,
                    LocationGUID = newStop.LocationGUID,
                    Item = newStop.Item,
                    Begin = newStop.Begin,
                    End = newStop.End,
                    RkItemStatus = newStop.RkItemStatus ?? 0
                };

                _db.YEStops.Add(stop);

                _db.YEStoplogs.Add(new YEStoplog
                {
                    LocationGUID = stop.LocationGUID,
                    Item = stop.Item,
                    Begin = stop.Begin,
                    End = stop.End,
                    User = model.User,
                    PermissionLevel = model.PermissionLevel,
                    Created = now,
                    Type = "Create"
                });
            }

            await _db.SaveChangesAsync();

            await UpdateYandexStopListAsync(pack);
            await PushUpdateStopSignal(pack);

            // Кассы (RK7) — только для глобальных стопов
            if (pack.PermissionLevel == 1)
                await _globalFunctionService.ProcessStopsAsync();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            var stopPack = await _db.YEStopPack
                .Include(x => x.Stops)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (stopPack == null)
                return NotFound();

            var now = DateTime.Now;

            foreach (var stop in stopPack.Stops)
            {
                // Лог удаления создаём ВСЕГДА
                _db.YEStoplogs.Add(new YEStoplog
                {
                    LocationGUID = stop.LocationGUID,
                    Item = stop.Item,
                    Begin = stop.Begin,
                    End = stopPack.PermissionLevel == 1
                        ? now
                        : stop.End,
                    User = stopPack.User,
                    PermissionLevel = stopPack.PermissionLevel,
                    Created = now,
                    Type = "Delete"
                });

                if (stopPack.PermissionLevel == 1)
                {
                    // Стоп мог быть установлен на кассах.
                    // Завершаем его, чтобы StopUpdateService
                    // снял его с касс.
                    stop.End = now;
                }
            }

            if (stopPack.PermissionLevel == 2)
            {
                // Этот тип стопа физически удаляем сразу.
                _db.YEStops.RemoveRange(stopPack.Stops);
                _db.YEStopPack.Remove(stopPack);
            }

            await _db.SaveChangesAsync();
            await UpdateYandexStopListAsync(stopPack);
            await PushUpdateStopSignal(stopPack);

            // Кассы (RK7) — только для глобальных стопов
            if (stopPack.PermissionLevel == 1)
                await _globalFunctionService.ProcessStopsAsync();

            return NoContent();
        }

        // Импорт стоп-листа в Яндекс не отправляем сразу:
        // отмечаем точку в буфере, YandexStopSyncService отправит
        // один импорт через 20 секунд после последнего изменения.
        private Task UpdateYandexStopListAsync(YEStopPack stopPack)
        {
            foreach (var locationGuid in stopPack.GetDistinctLocations())
            {
                _stopSyncBuffer.Touch(locationGuid);
            }

            return Task.CompletedTask;
        }

        private async Task PushUpdateStopSignal(YEStopPack stopPack)
        {
            var locations = stopPack.GetDistinctLocations();
            if (stopPack.PermissionLevel == 1)
            {
                foreach (var locationGuid in locations)
                {
                    await _cashSignalRService.StopsUpdate(locationGuid);
                }
            }
            if (stopPack.PermissionLevel == 2)
            {
                foreach (var locationGuid in locations)
                {
                    await _cashSignalRService.StopsUpdateWithoutNotifications(locationGuid);
                }
            }
        }

    }
}