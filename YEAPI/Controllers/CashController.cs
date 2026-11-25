using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using YEAPI.Attributes;
using YEAPI.Models;
using YEAPI.RK7;
using YEAPI.Services;

namespace YEAPI.Controllers
{
    [ApiController]
    [Route("api/cash")]
    public class CashController : ControllerBase
    {
        private readonly YEContext _db;
        private readonly RK7Service _rk7Service;
        private readonly CashSignalRService _cashSignalRService;
        public CashController(YEContext db, RK7Service rk7Service, CashSignalRService cashSignalRService)
        {
            _db = db;
            _rk7Service = rk7Service;
            _cashSignalRService = cashSignalRService;
        }

        [HttpGet("getlocations")]
        public async Task<ActionResult> GetLocations()
        {
            var locations = _db.Locations.Where(x => (x.LOCATIONTYPEGUID == Guid.Parse("94AD659C-AF5B-4CA0-50AD-08DBDF6ABE84") ||
                                                     x.LOCATIONTYPEGUID == Guid.Parse("3DC24D14-FAE6-4993-A403-C4142755409A") ||
                                                     x.LOCATIONTYPEGUID == Guid.Parse("B0E427F9-8996-4C03-33C1-08DBDF713401")) && x.ACTUAL == 1).ToList();
            return Ok(locations);
        }


        [HttpPost]
        public async Task<ActionResult<int>> Create(CreateCashRequest model)
        {
            var location = _db.Locations
                .FirstOrDefault(x => x.GUID == model.Location);

            if (location == null)
                return BadRequest("Точка продаж не найдена.");

            var settings = _db.YESettings
                .FirstOrDefault();

            if (settings == null)
                return BadRequest("Ошибка в settings.");

            var connection = new RK7Connection
            {
                Ip = model.Ip,
                Port = settings.RK7CashPort ?? 80,
                User = settings.RK7Login,
                Password = settings.RK7Pass
            };

            string xmlResponse;

            try
            {
                xmlResponse = RK7Functions.GetSystemInfo2(
                    _rk7Service,
                    connection);
            }
            catch (Exception ex)
            {
                return BadRequest($"Не удалось подключиться к кассе: {ex.Message}");
            }

            try
            {
                var xml = XDocument.Parse(xmlResponse);

                var result = xml.Root;

                if (result == null)
                    return BadRequest("RK7 вернул пустой ответ.");

                var status = result.Attribute("Status")?.Value;

                if (status != "Ok")
                {
                    var errorText = result.Attribute("ErrorText")?.Value;

                    return BadRequest(
                        $"RK7 вернул ошибку. Status: {status}, Error: {errorText}");
                }

                var cashGroup = result
                    .Element("SystemInfo")?
                    .Element("CashGroup");

                if (cashGroup == null)
                    return BadRequest("В ответе RK7 отсутствует CashGroup.");

                var cashGroupId = cashGroup.Attribute("id")?.Value;

                if (!int.TryParse(cashGroupId, out var midserver))
                    return BadRequest("Некорректный CashGroup id.");

                var cash = new YECashClient
                {
                    Ip = model.Ip,
                    LocationGUID = model.Location,
                    Name = location.NAME,
                    Midserver = midserver,
                    LastConnection = DateTime.Now
                };

                _db.YECashClients.Add(cash);

                await _db.SaveChangesAsync();

                return Ok(cash.Id);
            }
            catch (Exception ex)
            {
                return BadRequest($"Ошибка обработки ответа RK7: {ex.Message}");
            }
        }

        [HttpGet("{location}/orders/today")]
        public async Task<ActionResult<List<CashOrderDto>>> GetTodayOrders(Guid location)
        {
            var locationExists = await _db.Locations
                .AsNoTracking()
                .AnyAsync(x => x.GUID == location);

            if (!locationExists)
                return BadRequest("Точка продаж не найдена.");

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var orders = await _db.YEOrders
                .AsNoTracking()
                .Where(x =>
                    x.LocationGUID == location &&
                    x.Created >= today &&
                    x.Created < tomorrow)
                .Include(x => x.Items)
                    .ThenInclude(x => x.YEItem)
                .OrderBy(x => x.Created)
                .Select(x => new CashOrderDto
                {
                    OrderId = x.EatsId,
                    Platform = x.Platform,
                    Status = x.Status,
                    Created = x.Created,
                    Persons = x.Persons,
                    Comment = x.Comment,
                    Items = x.Items
                        .Select(i => new CashOrderItemDto
                        {
                            Item = i.Item,
                            Name = i.YEItem.YEName,
                            Quantity = i.Quantity
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }

        [HttpPost("order/{orderId}/accept")]
        [HttpLog]
        public async Task<IActionResult> AcceptOrder(string orderId)
        {
            var order = await _db.YEOrders
                .FirstOrDefaultAsync(x => x.EatsId == orderId);

            if (order == null)
                return NotFound("Заказ не найден.");

            order.Status = "READY";

            await _db.SaveChangesAsync();
            await _cashSignalRService.OrderStatusChanged(order.LocationGUID,order.EatsId);
            return Ok(new
            {
                result = "OK",
                orderId = order.EatsId,
                status = order.Status
            });
        }

        [HttpPost("order/{orderId}/cancel")]
        [HttpLog]
        public async Task<IActionResult> CancelOrder(string orderId)
        {
            var order = await _db.YEOrders
                .FirstOrDefaultAsync(x => x.EatsId == orderId);

            if (order == null)
                return NotFound("Заказ не найден.");

            order.Status = "CANCELLED";

            await _db.SaveChangesAsync();
            await _cashSignalRService.OrderCanseled(order.LocationGUID,order.EatsId);
            return Ok(new
            {
                result = "OK",
                orderId = order.EatsId,
                status = order.Status
            });
        }


        [HttpGet("getcashclients")]
        public async Task<ActionResult> GetCashClients()
        {
            var cashes = await _db.YECashClients
                .AsNoTracking()
                .ToListAsync();

            return Ok(cashes);
        }

        [HttpGet("getmenulastchange")]
        public async Task<ActionResult<DateTime>> GetMenuLastChange()
        {
            var lastChange = await _db.YESettings
                .AsNoTracking()
                .Where(x => x.Id == 1)
                .Select(x => (DateTime?)x.MenuLastChange)
                .FirstOrDefaultAsync();

            return Ok(lastChange ?? DateTime.MinValue);
        }

        [HttpPost("versions")]
        public async Task<ActionResult> CreateVersion(IFormFile file, [FromForm] string version, [FromForm] int actual)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Файл не выбран.");

            if (string.IsNullOrWhiteSpace(version))
                return BadRequest("Версия не указана.");

            if (actual == 1)
            {
                await _db.YECashVersions.ExecuteUpdateAsync(x => x.SetProperty(v => v.Actual, 0));
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            var cashVersion = new YECashVersion
            {
                File = stream.ToArray(),
                Version = version,
                Actual = actual
            };

            _db.YECashVersions.Add(cashVersion);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                cashVersion.Id,
                cashVersion.Version,
                cashVersion.Actual
            });
        }


        [HttpGet("versions")]
        public async Task<ActionResult> GetVersions()
        {
            var versions = await _db.YECashVersions
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Version,
                    x.Actual
                })
                .ToListAsync();

            return Ok(versions);
        }

        [HttpDelete("versions/{id:int}")]
        public async Task<ActionResult> DeleteVersion(int id)
        {
            var version = await _db.YECashVersions
                .FirstOrDefaultAsync(x => x.Id == id);

            if (version == null)
                return NotFound();

            _db.YECashVersions.Remove(version);

            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("versions/{id:int?}/download")]
        public async Task<ActionResult> DownloadVersion(int? id)
        {
            var version = await _db.YECashVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    id.HasValue
                        ? x.Id == id.Value
                        : x.Actual == 1);

            if (version == null)
                return NotFound();

            return File(
                version.File,
                "application/zip",
                $"CashClient_{version.Version}.zip");
        }

        [HttpPost("task")]
        public async Task<ActionResult<int>> CreateTask(
    int cashId,
    string type,
    int? versionId = null)
        {
            var cash = await _db.YECashClients
                .FirstOrDefaultAsync(x => x.Id == cashId);

            if (cash == null)
                return NotFound("Касса не найдена.");

            if (type != "Install" &&
                type != "Update" &&
                type != "Delete")
            {
                return BadRequest("Некорректный тип задания.");
            }

            YECashVersion? version = null;

            if (type == "Install")
            {
                version = await _db.YECashVersions
                    .FirstOrDefaultAsync(x => x.Actual == 1);

                if (version == null)
                    return BadRequest("Актуальная версия не найдена.");
            }
            else if (type == "Update")
            {
                if (!versionId.HasValue)
                    return BadRequest("Для Update необходимо указать versionId.");

                version = await _db.YECashVersions
                    .FirstOrDefaultAsync(x => x.Id == versionId.Value);

                if (version == null)
                    return BadRequest("Версия не найдена.");
            }

            var task = new YECashTask
            {
                CashId = cashId,
                Type = type,
                Stage = "Created",
                CashVersionId = version?.Id,
                StageChanged = DateTime.Now
            };

            _db.YECashTasks.Add(task);

            await _db.SaveChangesAsync();

            if (type == "Update")
            {
                await _cashSignalRService.CashUpdate(cashId, version!.Id, task.Id);
            }
            else if (type == "Delete")
            {
                await _cashSignalRService.CashDelete(cashId,task.Id);
            }
            return Ok(task.Id);
        }


        // GET UNFINISHED TASK
        [HttpGet("task/{cashId:int}")]
        public async Task<ActionResult> GetTask(int cashId)
        {
            var task = await _db.YECashTasks
                .AsNoTracking()
                .Where(x =>
                    x.CashId == cashId &&
                    x.Stage != "Completed")
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            if (task == null)
                return NotFound();

            return Ok(task);
        }


        // CHANGE TASK STAGE
        [HttpPut("task/{id:int}/stage")]
        public async Task<ActionResult> UpdateTaskStage(
    int id,
    UpdateCashTaskStageRequest model)
        {
            var task = await _db.YECashTasks
                .FirstOrDefaultAsync(x => x.Id == id);

            if (task == null)
                return NotFound("Задание не найдено.");

            task.Stage = model.Stage;
            task.StageChanged = DateTime.Now;

            if (model.Stage == "Completed" && task.CashVersionId.HasValue)
            {
                var cash = await _db.YECashClients
                    .FirstOrDefaultAsync(x => x.Id == task.CashId);

                if (cash == null)
                    return NotFound("Касса не найдена.");

                cash.CashVersion = task.CashVersionId.Value;
            }

            await _db.SaveChangesAsync();

            return Ok();
        }


        // DELETE INSTALLER
        [HttpPost("task/delete-installer")]
        public async Task<ActionResult> DeleteInstaller(
            int cashId,
            int taskId)
        {
            var task = await _db.YECashTasks
                .FirstOrDefaultAsync(x =>
                    x.Id == taskId &&
                    x.CashId == cashId);

            if (task == null)
                return NotFound("Задание не найдено.");

            if (task.Stage != "Ending_installer")
                return BadRequest("Задание не находится в состоянии Ending_installer.");

            var cash = await _db.YECashClients
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == cashId);

            if (cash == null)
                return NotFound("Касса не найдена.");

            var settings = await _db.YESettings
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (settings == null || string.IsNullOrWhiteSpace(settings.CashPath))
                return BadRequest("Путь CashPath не задан.");

            task.Stage = "Delete_installer";
            task.StageChanged = DateTime.Now;

            await _db.SaveChangesAsync();

            await Task.Delay(TimeSpan.FromSeconds(10));

            var path = settings.CashPath.Replace("{ip}", cash.Ip);

            if (Directory.Exists(path))
                Directory.Delete(path, true);

            // Удаляем кассу из БД
            _db.YECashClients.Remove(cash);

            task.Stage = "Completed";
            task.StageChanged = DateTime.Now;

            await _db.SaveChangesAsync();

            return Ok();
        }

    }

    public class CreateCashRequest
    {
        public string Ip { get; set; } = null!;

        public Guid Location { get; set; }
    }

    public class CashOrderDto
    {
        public string OrderId { get; set; } = null!;

        public string Platform { get; set; } = null!;

        public string Status { get; set; } = null!;

        public DateTime Created { get; set; }
        public int? Persons { get; set; }
        public string? Comment { get; set; }

        public List<CashOrderItemDto> Items { get; set; } = new();
    }

    public class CashOrderItemDto
    {
        public int Item { get; set; }

        public string Name { get; set; } = null!;

        public int Quantity { get; set; }
    }

    public class UpdateCashTaskStageRequest
    {
        public string Stage { get; set; } = null!;
    }
}
