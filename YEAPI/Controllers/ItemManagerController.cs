using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime;
using System.Security.Cryptography;
using YEAPI.Attributes;
using YEAPI.Models;
using YEAPI.RK7;
using YEAPI.Services;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace YEAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ItemManager : ControllerBase
    {
        private readonly YEContext _db;
        private readonly RK7Service _rk7Service;
        private readonly YandexEatsService _yandexEatsService;
        private readonly FsaService _fsaService;
        public ItemManager(YEContext db, RK7Service rk7Service, YandexEatsService yandexEatsService, FsaService fsaService)
        {
            _db = db;
            _rk7Service = rk7Service;
            _yandexEatsService = yandexEatsService;
            _fsaService = fsaService;
        }
        [HttpGet("getmenu")]
        public ActionResult<RK7Reference> GetMenu()
        {
            YESettings settings = _db.YESettings.FirstOrDefault();
            if (settings == null)
            {
                return BadRequest(new
                {
                    Message = "Ошибка в settings"
                });
            }
            var connection = new RK7Connection { Ip = settings.RK7Server!, Port = settings.RK7ServerPort ?? 3355, User = settings.RK7Login!, Password = settings.RK7Pass! };
            try
            {
                var menu = RK7Functions.GetMenu(_rk7Service, connection);

                if (menu == null || menu.Categories.Count == 0)
                    return NotFound();

                return Ok(menu);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = ex.Message
                });
            }
        }

        [HttpGet("getallmenu")]
        public ActionResult<RK7Reference> GetAllMenu()
        {
            YESettings settings = _db.YESettings.FirstOrDefault();
            if (settings == null)
            {
                return BadRequest(new
                {
                    Message = "Ошибка в settings"
                });
            }
            var connection = new RK7Connection { Ip = settings.RK7Server!, Port = settings.RK7ServerPort ?? 3355, User = settings.RK7Login!, Password = settings.RK7Pass! };
            try
            {
                var menu = RK7Functions.GetAllMenu(_rk7Service, connection);

                if (menu == null || menu.Categories.Count == 0)
                    return NotFound();

                return Ok(menu);
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = ex.Message
                });
            }
        }


        [HttpGet("getallgroups")]
        public async Task<ActionResult<List<YEGroup>>> GetAllGroups()
        {
            var groups = await _db.YEGroups.Where(x => x.Actual == 1).ToListAsync();
            if (groups == null)
                return NotFound();
            return Ok(groups);
        }

        [HttpPost("group")]
        public async Task<ActionResult> Create(YEGroup group)
        {
            group.ImgUpdated = DateTime.Now;
            _db.Set<YEGroup>().Add(group);
            await TouchMenuLastChangeAsync();
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpPut("group/{id}")]
        public async Task<ActionResult> Update(int id, YEGroup model)
        {
            var group = await _db.Set<YEGroup>()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (group == null)
                return NotFound();
            group.YaName = model.YaName;
            group.Image = model.Image;
            group.ImgUpdated = DateTime.Now;
            await TouchMenuLastChangeAsync();
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("groups/{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var group = await _db.Set<YEGroup>()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (group == null)
                return NotFound();
            if (group.Items.Count > 0)
                return BadRequest(new {Message = "Возможно удаление только пустых групп"});
            group.ImgUpdated = DateTime.Now;
            group.Actual = 0;
            await TouchMenuLastChangeAsync();
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("groups/{groupId}/items")]
public async Task<ActionResult<List<YEItem>>> GetGroupItems(int groupId)
{
    try
    {
        var items = await _db.YEItems
            .AsNoTracking()
            .Include(x => x.YEDeclaration)
            .Where(x => x.YEGroup == groupId)
            .ToListAsync();

        if (items.Count == 0)
            return NotFound();

        return Ok(items);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new
        {
            message = ex.Message,
            innerException = ex.InnerException?.Message,
            stackTrace = ex.StackTrace
        });
    }
}

        [HttpGet("groups/{groupId}/items/actual")]
        public async Task<ActionResult<List<YEItem>>> GetActualGroupItems(int groupId)
        {
            var items = await _db.YEItems
                .Include(x => x.YEDeclaration)
                .Where(x => x.YEGroup == groupId && x.Actual == 1)
                .ToListAsync();
            if (items == null || items.Count < 1)
                return NotFound();
            return Ok(items);
        }

        [HttpGet("item/{id}")]
        public async Task<ActionResult<YEItem>> GetItem(int id)
        {
            var item = await _db.YEItems
                .Include(x => x.YEDeclaration)
                .FirstOrDefaultAsync(x => x.Rkcode == id);
            if (item == null)
                return NotFound();
            return Ok(item);
        }

        [HttpPost("item")]
        public async Task<ActionResult> CreateItem(YEItem model)
        {
            if (model == null)
                return BadRequest();

            if (model.Image == null || model.Image.Length == 0)
                return BadRequest(new
                {
                    message = "Изображение обязательно."
                });

            model.ImageHash = CalculateImageHash(model.Image);

            // Декларация привязывается только по Id (Declaration).
            // Вложенный объект не принимаем, иначе EF попытается его создать.
            model.YEDeclaration = null;

            _db.YEItems.Add(model);

            await TouchMenuLastChangeAsync();
            await _db.SaveChangesAsync();

            await _db.Entry(model)
                .Reference(x => x.YEDeclaration)
                .LoadAsync();

            return Ok(model);
        }

        [HttpPut("items/{id}")]
        public async Task<ActionResult> UpdateItem(int id, YEItem model)
        {
            var item = await _db.YEItems
                .FirstOrDefaultAsync(x => x.Rkcode == id);

            if (item == null)
                return NotFound();

            item.YEGroup = model.YEGroup;
            item.RkName = model.RkName;
            item.YEName = model.YEName;
            item.Description = model.Description;
            item.Price = model.Price;
            item.Measure = model.Measure;
            item.MeasureUnit = model.MeasureUnit;

            // Привязка декларации: null — отвязать
            item.Declaration = model.Declaration;
            item.TnvedCode = model.TnvedCode;

            if (model.Image != null && model.Image.Length > 0)
            {
                item.Image = model.Image;
                item.ImageHash = CalculateImageHash(model.Image);
            }
            await TouchMenuLastChangeAsync();
            await _db.SaveChangesAsync();
            await UpdateYandexMenuAsync();
            return NoContent();
        }

        [HttpPut("items/{id}/actual")]
        public async Task<ActionResult> SetItemActual(int id)
        {
            var item = await _db.YEItems
                .FirstOrDefaultAsync(x => x.Rkcode == id);
            if (item == null)
                return NotFound();
            item.Actual = 1;
            await TouchMenuLastChangeAsync();
            await _db.SaveChangesAsync();
            await UpdateYandexMenuAsync();
            return NoContent();
        }

        [HttpPut("items/{id}/unactual")]
        public async Task<ActionResult> SetItemUnactual(int id)
        {
            var item = await _db.YEItems
                .FirstOrDefaultAsync(x => x.Rkcode == id);
            if (item == null)
                return NotFound();
            item.Actual = 0;
            await TouchMenuLastChangeAsync();
            await _db.SaveChangesAsync();
            await UpdateYandexMenuAsync();
            return NoContent();
        }


        // ==========================================================
        // Поиск декларации во ФГИС для предпросмотра.
        // Ничего не сохраняет.
        // ==========================================================

        [HttpPost("declarations/lookup")]
        public async Task<ActionResult<DeclarationLookupResponse>> LookupDeclaration(
            DeclarationLookupRequest model)
        {
            if (string.IsNullOrWhiteSpace(model?.DocNumber))
                return BadRequest(new { message = "Не указан номер декларации." });

            try
            {
                var declaration = await _fsaService.LookupDeclarationAsync(model.DocNumber);

                if (declaration == null)
                    return NotFound(new { message = "Декларация не найдена во ФГИС." });

                return Ok(declaration);
            }
            catch (Exception ex)
            {
                return StatusCode(502, new { message = ex.Message });
            }
        }

        // ==========================================================
        // Сохранение декларации (данные присылает фронт)
        // ==========================================================

        [HttpPost("declarations")]
        public async Task<ActionResult<YEDeclaration>> CreateDeclaration(
            YEDeclaration model)
        {
            var exists = await _db.YEDeclarations
                .AnyAsync(x => x.DocNumber == model.DocNumber);

            if (exists)
                return Conflict(new { message = "Декларация с таким номером уже сохранена." });

            model.Id = 0;

            _db.YEDeclarations.Add(model);

            await _db.SaveChangesAsync();

            return Ok(model);
        }

        // ==========================================================
        // Актуальные декларации — срок действия не истёк
        // ==========================================================

        [HttpGet("declarations/actual")]
        public async Task<ActionResult<List<YEDeclaration>>> GetActualDeclarations()
        {
            var today = DateTime.Today;

            var declarations = await _db.YEDeclarations
                .AsNoTracking()
                .Where(x => x.DateTo == null || x.DateTo >= today)
                .OrderBy(x => x.DocNumber)
                .ToListAsync();

            return Ok(declarations);
        }

        private static string CalculateImageHash(byte[] image)
        {
            using var sha1 = SHA1.Create();

            var hash = sha1.ComputeHash(image);

            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private async Task TouchMenuLastChangeAsync()
        {
            var settings = await _db.YESettings
                .FirstOrDefaultAsync(x => x.Id == 1);

            if (settings != null)
                settings.MenuLastChange = DateTime.Now;
        }

        private async Task UpdateYandexMenuAsync()
        {
            var locations = await _db.Locations
                .Where(x =>
                    (
                        x.LOCATIONTYPEGUID == Guid.Parse("94AD659C-AF5B-4CA0-50AD-08DBDF6ABE84") ||
                        x.LOCATIONTYPEGUID == Guid.Parse("3DC24D14-FAE6-4993-A403-C4142755409A") ||
                        x.LOCATIONTYPEGUID == Guid.Parse("B0E427F9-8996-4C03-33C1-08DBDF713401")
                    ) && x.ACTUAL == 1 && x.Delivery == 1)
                .ToListAsync();

            foreach (var location in locations)
            {
                await _yandexEatsService.InitiateMenuImportAsync(
                     location.GUID,
                    "menu");
            }
        }
    }
}
