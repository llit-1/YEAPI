using System.Collections.Concurrent;

namespace YEAPI.Services
{
    // ==============================================================
    // Буфер отправки стоп-листа в Яндекс (debounce по точке)
    //
    // Продавец ставит стопы массово — по одному запросу на позицию.
    // Вместо импорта на каждый запрос запоминаем время последнего
    // изменения по точке. YandexStopSyncService отправит один импорт,
    // когда по точке 20 секунд не было изменений.
    // ==============================================================

    public class YandexStopSyncBuffer
    {
        // Точка → время последнего изменения стопов (UTC)
        private readonly ConcurrentDictionary<Guid, DateTime> _lastChange = new();

        // Отметить изменение стопов по точке. Ничего не ждёт.
        public void Touch(Guid locationGuid)
        {
            _lastChange[locationGuid] = DateTime.UtcNow;
        }

        // Забрать точки, по которым изменений не было дольше quiet
        public List<Guid> TakeReady(TimeSpan quiet)
        {
            var now = DateTime.UtcNow;
            var ready = new List<Guid>();

            foreach (var pair in _lastChange)
            {
                if (now - pair.Value < quiet)
                    continue;

                // Удаляем, только если за это время не было нового Touch
                if (_lastChange.TryRemove(pair))
                    ready.Add(pair.Key);
            }

            return ready;
        }
    }
}
