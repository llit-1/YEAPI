using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YEAPI.Models;
using YEAPI.RK7;
using YEAPI.Services;

namespace YEAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly YEContext _db;
        private readonly RK7Service _rk7Service;
        private readonly CashSignalRService _cashSignalRService;

        public MessageController(
            YEContext db,
            RK7Service rk7Service,
            CashSignalRService cashSignalRService)
        {
            _db = db;
            _rk7Service = rk7Service;
            _cashSignalRService = cashSignalRService;
        }

        // ============================================================
        // Отправка сообщения на все кассы точки
        // ============================================================

        [HttpPost("send")]
        public async Task<IActionResult> Send(
            [FromBody] CashMessageRequest request)
        {
            if (request == null)
                return BadRequest();
            if (request.Location == Guid.Empty)
            {
                return BadRequest(new
                {
                    message = "GUID точки не указан."
                });
            }
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new
                {
                    message = "Сообщение не указано."
                });
            }
            // ========================================================
            // Настройки RK7
            // ========================================================

            var settings = await _db.YESettings.AsNoTracking().FirstOrDefaultAsync();
            if (settings == null)
            {
                return BadRequest(new
                {
                    message = "Настройки RK7 не найдены."
                });
            }

            if (string.IsNullOrWhiteSpace(settings.RK7Login) ||
                string.IsNullOrWhiteSpace(settings.RK7Pass))
            {
                return BadRequest(new
                {
                    message = "Не настроены логин или пароль RK7."
                });
            }

            // ========================================================
            // Получаем кассы данной точки
            // ========================================================

            var cashes = await _db.YECashClients
                .AsNoTracking()
                .Where(x => x.LocationGUID == request.Location)
                .ToListAsync();

            if (cashes.Count == 0)
            {
                return NotFound(new
                {
                    message = "У данной точки нет зарегистрированных касс."
                });
            }

            var results = new List<object>();

            // ========================================================
            // Отправляем сообщение каждой кассе
            // ========================================================

            foreach (var cash in cashes)
            {
                string? xmlResponse = null;
                string? xmlError = null;
                bool signalRSuccess = false;
                string? signalRError = null;

                // ============================================================
                // Отправка сообщения в RK7
                // ============================================================

                try
                {
                    var connection = new RK7Connection
                    {
                        Ip = cash.Ip,
                        Port = settings.RK7CashPort ?? 80,
                        User = settings.RK7Login,
                        Password = settings.RK7Pass
                    };

                    xmlResponse =
                        RK7Functions.SendWaiterMessage(
                            _rk7Service,
                            connection,
                            request.Message,
                            30);
                }
                catch (Exception ex)
                {
                    xmlError = ex.Message;
                }

                // ============================================================
                // Отправка Push через SignalR
                // ============================================================

                try
                {
                    await _cashSignalRService.SendToCash(
                        cash.Id,
                        request.Message);

                    signalRSuccess = true;
                }
                catch (Exception ex)
                {
                    signalRError = ex.Message;
                }

                // ============================================================
                // Результат по кассе
                // ============================================================

                results.Add(new
                {
                    cashId = cash.Id,
                    ip = cash.Ip,

                    xml = new
                    {
                        success = xmlError == null,
                        response = xmlResponse,
                        error = xmlError
                    },

                    signalR = new
                    {
                        success = signalRSuccess,
                        error = signalRError
                    }
                });
            }

            return Ok(new
            {
                location = request.Location,
                message = request.Message,
                cashes = results
            });
        }
    }

    public class CashMessageRequest
    {
        public Guid Location { get; set; }

        public string Message { get; set; } = null!;
    }
}