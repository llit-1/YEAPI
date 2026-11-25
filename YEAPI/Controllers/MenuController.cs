using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YEAPI.Attributes;
using YEAPI.Models;

namespace YEAPI.Controllers;

[ApiController]
[Route("menu")]
public class MenuController : ControllerBase
{
    private readonly YEContext _db;

    public MenuController(YEContext db)
    {
        _db = db;
    }


    [HttpLog]
    [HttpGet("{restaurantId}/composition")]
    [Produces("application/vnd.eats.menu.composition.v2+json")]
    public async Task<IActionResult> GetMenu(string restaurantId)
    {
        // ==================================================
        // Проверяем restaurantId
        // ==================================================

        if (!Guid.TryParse(restaurantId, out var locationGuid))
        {
            return BadRequest(new[]
            {
            new
            {
                code = 400,
                description = "Invalid restaurantId."
            }
        });
        }

        var locationExists = await _db.Locations.AsNoTracking().AnyAsync(x => x.GUID == locationGuid);
        if (!locationExists)
        {
            return BadRequest(new[]
            {
                new
                {
                    code = 400,
                    description = "Invalid restaurantId."
                }
            });
        }



        // ==================================================
        // Загружаем актуальные группы и блюда
        // ==================================================

        var groups = await _db.YEGroups
            .AsNoTracking()
            .Where(x => x.Actual != 0)
            .Include(x => x.Items)
                .ThenInclude(x => x.YEDeclaration)
            .ToListAsync();

        var today = DateTime.Today;

        // ==================================================
        // Оставляем только категории,
        // в которых есть хотя бы одно актуальное блюдо
        // с ценой > 0
        // ==================================================

        var validGroups = groups
            .Where(group => group.Items.Any(item =>
                item.Actual != 0 &&
                item.Price > 0))
            .ToList();

        // ==================================================
        // Категории
        // ==================================================

        var categories = validGroups
            .Select(group => new MenuCategoryDto
            {
                id = group.Id.ToString(),
                name = group.YaName,
                parentId = null,
                sortOrder = group.Id,
                images = group.Image != null && group.Image.Length > 0
                    ? new List<MenuCategoryImageDto>
                        {
                             new MenuCategoryImageDto
                                {
                                     url = $"https://yeapi.ludilove.ru/menu/groups/{group.Id}/image",
                                     updatedAt = group.ImgUpdated
                                }
                        }: null
            })
            .ToList();

        // ==================================================
        // Блюда
        // ==================================================

        var items = validGroups
            .SelectMany(group =>
                group.Items
                    .Where(item =>
                        item.Actual != 0 &&
                        item.Price > 0)
                    .Select(item => new MenuItemDto
                    {
                        categoryId = group.Id.ToString(),
                        id = item.Rkcode.ToString(),
                        measure = item.Measure,
                        measureUnit = item.MeasureUnit,
                        name = item.YEName,
                        price = item.Price,
                        description = item.Description,
                        isCatchweight = false,
                        sortOrder = item.Rkcode,
                        images = item.Image != null && item.Image.Length > 0? new List<MenuItemImageDto>
                        {
                            new MenuItemImageDto
                                {
                                    hash = item.ImageHash,
                                    url = $"https://yeapi.ludilove.ru/menu/items/{item.Rkcode}/image"
                                }
                        }: null,
                        legalInfo = BuildLegalInfo(item, today)
                    }))
            .ToList();

        // ==================================================
        // Получаем дату последнего изменения меню
        // ==================================================

        var settings = await _db.YESettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1);

        if (settings == null)
        {
            return StatusCode(500, new[]
            {
                new
                {
                    code = 500,
                    description = "YESettings is not configured."
                }
            });
        }

        // ==================================================
        // Формируем ответ
        // ==================================================

        var result = new MenuCompositionDto
        {
            categories = categories,
            items = items,
            lastChange = settings.MenuLastChange
        };
        return Ok(result);
    }

    // ==================================================
    // legalInfo позиции меню
    //
    // Передаём, только если у блюда есть действующая
    // декларация. Иначе — null (поле не попадёт в ответ).
    // ==================================================

    private static MenuItemLegalInfoDto? BuildLegalInfo(YEItem item, DateTime today)
    {
        //var declaration = item.YEDeclaration;

        //if (declaration == null)
        //    return null;

        //if (declaration.DateTo.HasValue && declaration.DateTo.Value.Date < today)
        //    return null;

        //var productionAddress = declaration.ProductionAddress?
        //    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        //    .ToList();

        return new MenuItemLegalInfoDto
        {
            //classificationCodes = string.IsNullOrWhiteSpace(item.TnvedCode)
            //    ? null
            //    : new MenuClassificationCodesDto
            //    {
            //        tnvedCode = item.TnvedCode.Trim()
            //    },

            classificationCodes = new MenuClassificationCodesDto(),

            conformityInfo = new MenuConformityInfoDto
            {
                status = "not_required",

                //documents = new MenuConformityDocumentsDto
                //{
                //    declarations =
                //    [
                //        new MenuDeclarationDto
                //        {
                //            declarationNumber = declaration.DocNumber,

                //            declaration = declaration.FsaId.HasValue
                //                ? $"https://pub.fsa.gov.ru/rds/declaration/view/{declaration.FsaId}/common"
                //                : null,

                //            declarationDateFrom = declaration.DateFrom?.ToString("yyyy-MM-dd"),

                //            declarationDateTo = declaration.DateTo?.ToString("yyyy-MM-dd"),

                //            manufacturerInfo =
                //            [
                //                new MenuManufacturerInfoDto
                //                {
                //                    manufacturerName = declaration.ManufacturerName,
                //                    legalAddress = declaration.LegalAddress,
                //                    productionAddress = productionAddress?.Count > 0
                //                        ? productionAddress
                //                        : null
                //                }
                //            ]
                //        }
                //    ]
                //}
            },

            governmentRegistrationInfo = new MenuGovernmentRegistrationInfoDto
            {
                status = "not_required"
            }
        };
    }

    [AllowAnonymous]
    [HttpGet("items/{id}/image")]
    public async Task<IActionResult> GetItemImage(int id)
    {
        var item = await _db.YEItems
            .AsNoTracking()
            .Where(x => x.Rkcode == id)
            .Select(x => new
            {
                x.Image
            })
            .SingleOrDefaultAsync();

        if (item == null || item.Image == null || item.Image.Length == 0)
            return NotFound();

        return File(item.Image, "image/jpeg");
    }

    [AllowAnonymous]
    [HttpGet("groups/{id}/image")]
    public async Task<IActionResult> GetGroupImage(int id)
    {
        var image = await _db.YEGroups
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.Image)
            .SingleOrDefaultAsync();

        if (image == null || image.Length == 0)
            return NotFound();

        return File(image, "image/jpeg");
    }

    [Authorize]
    [HttpGet("{restaurantId}/availability")]
    [Produces("application/vnd.eats.menu.availability.v2+json")]
    public async Task<IActionResult> GetAvailability(string restaurantId)
    {
        // ==================================================
        // Проверяем restaurantId
        // ==================================================

        if (!Guid.TryParse(restaurantId, out var locationGuid))
        {
            return BadRequest(new[]
            {
            new
            {
                code = 400,
                description = "Invalid restaurantId."
            }
        });
        }
        var locationExists = await _db.Locations.AsNoTracking().AnyAsync(x => x.GUID == locationGuid);
        if (!locationExists)
        {
            return BadRequest(new[]
            {
                new
                {
                    code = 400,
                    description = "Invalid restaurantId."
                }
            });
        }


        // ==================================================
        // Текущее время
        // ==================================================

        var now = DateTime.Now;

        // ==================================================
        // Получаем актуальные стопы ресторана
        // ==================================================

        var stops = await _db.YEStops
            .AsNoTracking()
            .Where(x =>
                x.LocationGUID == locationGuid &&
                x.Begin <= now &&
                x.End >= now)
            .ToListAsync();

        // ==================================================
        // Формируем список недоступных блюд
        // ==================================================

        var items = stops
            .Select(x => x.Item.ToString())
            .Distinct()
            .Select(itemId => new MenuAvailabilityItemDto
            {
                itemId = itemId,
                stock = 0
            })
            .ToList();

        // ==================================================
        // Ответ
        // ==================================================

        var result = new MenuAvailabilityDto
        {
            items = items,
            modifiers = [],
            combos = []
        };

        return Ok(result);
    }

}