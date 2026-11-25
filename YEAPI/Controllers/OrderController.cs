using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using YEAPI.Attributes;
using YEAPI.Models;
using YEAPI.Services;

namespace YEAPI.Controllers
{
    [ApiController]
    [Route("order")]
    public class OrderController : ControllerBase
    {
        private readonly YEContext _db;
        private readonly CashSignalRService _cashSignalR;

        public OrderController(YEContext db, CashSignalRService cashSignalR)
        {
            _db = db;
            _cashSignalR = cashSignalR;
        }

        [Authorize]
        [HttpPost]
        [HttpLog]
        public async Task<IActionResult> CreateOrder([FromBody] OrderRequest model)
        {
            if (model == null)
                return BadRequest();

            // ==================================================
            // Проверяем restaurantId
            // ==================================================

            if (!Guid.TryParse(model.RestaurantId, out var locationGuid))
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





            // ==========================================================
            // Получаем ID блюд из заказа
            // ==========================================================

            var itemIds = new List<int>();

            foreach (var item in model.Items)
            {
                if (!int.TryParse(item.Id, out var itemId))
                {
                    return BadRequest(new[]
                    {
                        new
                        {
                            code = 100,
                            description = $"Некорректный id товара: {item.Id}"
                        }
                    });
                }

                itemIds.Add(itemId);
            }

            // ==========================================================
            // Проверяем существование товаров
            // ==========================================================

            var existingItemIds = await _db.YEItems
                .Where(x => itemIds.Contains(x.Rkcode))
                .Select(x => x.Rkcode)
                .ToListAsync();

            var missingItemIds = itemIds
                .Distinct()
                .Except(existingItemIds)
                .ToList();

            if (missingItemIds.Count > 0)
            {
                return BadRequest(new[]
                {
                    new
                        {
                            code = 100,
                            description = $"Товар не найден: {string.Join(", ", missingItemIds)}"
                        }
                });
            }


            // ==========================================================
            // Проверяем блюда на стоп
            // ==========================================================

            var now = DateTime.Now;

            var stoppedItems = await _db.YEStops
                .Where(x =>
                    x.LocationGUID == locationGuid &&
                    itemIds.Contains(x.Item) &&
                    x.Begin <= now &&
                    x.End >= now)
                .Include(x => x.YEItem)
                .Select(x => new
                {
                    Id = x.Item,
                    Name = x.YEItem!.YEName
                })
                .ToListAsync();

            // Группируем по блюду.
            // Если для одного блюда есть несколько записей стопа,
            // оно всё равно попадёт в ответ только один раз.
            var stoppedGoods = stoppedItems
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToList();

            if (stoppedGoods.Count > 0)
            {
                var goods = stoppedGoods.ToDictionary(
                    x => x.Id.ToString(),
                    x => x.Name);

                var names = stoppedGoods
                    .Select(x => x.Name)
                    .ToList();

                // goods — объект { "id": "название" }, не строка
                return StatusCode(
                    StatusCodes.Status406NotAcceptable,
                    new
                    {
                        type = "unavailable_goods",

                        message =
                            $"На данный момент товар недоступен для заказа: {string.Join(", ", names)}",

                        goods
                    });
            }

            // ==========================================================
            // Ищем существующий заказ
            // ==========================================================

            var existingOrder = await _db.YEOrders
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.EatsId == model.EatsId);

            // ==========================================================
            // ЗАКАЗ УЖЕ СУЩЕСТВУЕТ
            // ==========================================================

            if (existingOrder != null)
            {
                // Состав существующего заказа
                var existingItems = existingOrder.Items
                    .Select(x => new
                    {
                        Item = x.Item,
                        Quantity = x.Quantity
                    })
                    .OrderBy(x => x.Item)
                    .ToList();

                // Состав заказа от Яндекса
                var newItems = new List<(int Item, int Quantity)>();

                foreach (var item in model.Items)
                {
                    var itemId = int.Parse(item.Id);

                    newItems.Add((itemId, (int)item.Quantity));
                }

                var incomingItems = newItems
                    .OrderBy(x => x.Item)
                    .ToList();

                // ======================================================
                // Сравниваем составы
                // ======================================================

                bool itemsEqual =
                    existingItems.Count == incomingItems.Count &&
                    existingItems.Zip(incomingItems)
                        .All(x =>
                            x.First.Item == x.Second.Item &&
                            x.First.Quantity == x.Second.Quantity);

                // ======================================================
                // Состав совпадает — ничего не меняем
                // ======================================================

                if (itemsEqual)
                {
                    return Ok(new
                    {
                        result = "OK",
                        orderId = model.EatsId
                    });
                }

                // ======================================================
                // Состав отличается.
                // Яндекс является источником истины.
                // Заменяем наш состав составом из запроса.
                // ======================================================

                _db.YEOrderItems.RemoveRange(existingOrder.Items);

                existingOrder.Items.Clear();

                foreach (var item in incomingItems)
                {
                    existingOrder.Items.Add(new YEOrderItem
                    {
                        Order = existingOrder.EatsId,
                        Item = item.Item,
                        Quantity = item.Quantity
                    });
                }

                await _db.SaveChangesAsync();
                await _cashSignalR.OrderChanged(locationGuid,model.EatsId);
                return Ok(new
                {
                    result = "OK",
                    orderId = model.EatsId
                });
            }

            // ==========================================================
            // НОВЫЙ ЗАКАЗ
            // ==========================================================

            var order = new YEOrder
            {
                EatsId = model.EatsId,
                Platform = model.Platform,
                Discriminator = model.Discriminator,
                LocationGUID = locationGuid,
                Persons = model.Persons,
                Comment = model.Comment,
                Status = "NEW",
                Created = DateTime.Now
            };

            foreach (var item in model.Items)
            {
                var itemId = int.Parse(item.Id);

                order.Items.Add(new YEOrderItem
                {
                    Order = model.EatsId,
                    Item = itemId,
                    Quantity = (int)item.Quantity
                });
            }

            _db.YEOrders.Add(order);

            await _db.SaveChangesAsync();
            await _cashSignalR.NewOrder(locationGuid, model.EatsId);
            // ==========================================================
            // Успешное создание
            // ==========================================================
            return Ok(new
            {
                result = "OK",
                orderId = model.EatsId
            });
        }
        [Authorize]
        [HttpPut("{orderId}")]
        [HttpLog]
        public async Task<IActionResult> UpdateOrder(
        string orderId,
        [FromBody] OrderRequest model)
        {
            if (model == null)
                return BadRequest();

            // ==================================================
            // Проверяем restaurantId
            // ==================================================

            if (!Guid.TryParse(model.RestaurantId, out var locationGuid))
            {
                return BadRequest(new[]
                {
                    new
                        {
                            code = 100,
                            description = "Invalid restaurantId."
                        }
                });
            }

            var locationExists = await _db.Locations
                .AsNoTracking()
                .AnyAsync(x => x.GUID == locationGuid);

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
            // Проверяем orderId
            // ==================================================

            if (string.IsNullOrWhiteSpace(orderId))
            {
                return BadRequest(new[]
                {
                    new
                    {
                        code = 400,
                        description = "Некорректный orderId."
                    }
                });
            }

            // ==================================================
            // Проверяем eatsId
            // ==================================================

            if (string.IsNullOrWhiteSpace(model.EatsId))
            {
                return BadRequest(new[]
                {
            new
            {
                code = 400,
                description = "Некорректный eatsId."
            }
        });
            }

            // ==================================================
            // Ищем заказ
            // ==================================================

            var order = await _db.YEOrders
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.EatsId == orderId);

            if (order == null)
            {
                return NotFound(new[]
                {
            new
            {
                code = 404,
                description = "Заказ не найден."
            }
        });
            }

            // ==================================================
            // Проверяем ID товаров
            // ==================================================

            var itemIds = new List<int>();

            foreach (var item in model.Items)
            {
                if (!int.TryParse(item.Id, out var itemId))
                {
                    return BadRequest(new[]
                    {
                new
                {
                    code = 400,
                    description = $"Некорректный id товара: {item.Id}"
                }
            });
                }

                itemIds.Add(itemId);
            }

            // ==================================================
            // Проверяем существование товаров
            // ==================================================

            var existingItemIds = await _db.YEItems
                .Where(x => itemIds.Contains(x.Rkcode))
                .Select(x => x.Rkcode)
                .ToListAsync();

            var missingItemIds = itemIds
                .Distinct()
                .Except(existingItemIds)
                .ToList();

            if (missingItemIds.Count > 0)
            {
                return BadRequest(new[]
                {
            new
            {
                code = 400,
                description =
                    $"Товар не найден: {string.Join(", ", missingItemIds)}"
            }
        });
            }

            // Стоп-лист при изменении заказа Яндексом не проверяем:
            // заказ уже принят, Яндекс является источником истины.

            // ==================================================
            // Заменяем состав заказа
            // ==================================================

            _db.YEOrderItems.RemoveRange(order.Items);

            order.Items.Clear();

            foreach (var item in model.Items)
            {
                var itemId = int.Parse(item.Id);

                order.Items.Add(new YEOrderItem
                {
                    Order = order.EatsId,
                    Item = itemId,
                    Quantity = (int)item.Quantity
                });
            }

            // ==================================================
            // Сохраняем
            // ==================================================

            await _db.SaveChangesAsync();
            await _cashSignalR.OrderChanged(locationGuid,model.EatsId);
            // ==================================================
            // Успешное обновление
            // ==================================================

            return Ok(new
            {
                result = "OK"
            });
        }

        [Authorize]
        [HttpDelete("{orderId}")]
        [HttpLog]
        public async Task<IActionResult> CancelOrder(string orderId,[FromBody] OrderCancelRequest model)
        {
            if (model == null)
                return BadRequest();

            // Ищем заказ по orderId из URL
            var order = await _db.YEOrders
                .FirstOrDefaultAsync(x => x.EatsId == orderId);

            if (order == null)
            {
                return NotFound(new[]
                {
            new
            {
                code = 404,
                description = "Заказ не найден."
            }
                });
            }

            // Отменяем заказ
            order.Status = "CANCELLED";
            order.Comment = model.Comment;
            await _db.SaveChangesAsync();

            // LocationGUID уже есть в сохранённом заказе
            await _cashSignalR.OrderCanseled(
                order.LocationGUID,           
                order.EatsId);

            return Ok();
        }

        [Authorize]
        [HttpPut("{orderId}/status")]
        [HttpLog]
        public async Task<IActionResult> UpdateOrderStatus(
        string orderId,
        [FromBody] OrderStatusRequest model)
        {
            if (model == null)
            {
                return BadRequest(new[]
                {
            new
            {
                code = 400,
                description = "Некорректное тело запроса."
            }
                });
            }

           
            // ==========================================================
            // Ищем заказ
            // ==========================================================

            var order = await _db.YEOrders
                .FirstOrDefaultAsync(x => x.EatsId == orderId);

            if (order == null)
            {
                return NotFound(new[]
                {
                    new
                    {
                        code = 404,
                        description = "Заказ не найден."
                    }
                });
            }

            // ==========================================================
            // Обновляем статус
            // ==========================================================

            order.Status = model.Status;

            await _db.SaveChangesAsync();
            await _cashSignalR.OrderStatusChanged(order.LocationGUID,order.EatsId);
            // ==========================================================
            // Успешно
            // ==========================================================

            return NoContent();
        }

        [Authorize]
        [HttpPut("{orderId}/courier")]
        [HttpLog]
        public IActionResult UpdateCourier(string orderId)
        {
            return NoContent();
        }


        [Authorize]
        [HttpGet("{orderId}/status")]
        public async Task<IActionResult> GetOrderStatus(string orderId)
        {
            // ==========================================================
            // Ищем заказ
            // ==========================================================

            var order = await _db.YEOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.EatsId == orderId);

            if (order == null)
            {
                return NotFound(new[]
                {
            new
            {
                code = 404,
                description = "Заказ не найден."
            }
        });
            }

            // ==========================================================
            // Возвращаем статус
            // ==========================================================

            return Ok(new
            {
                status = order.Status,
                comment = "",
                updatedAt = DateTime.Now,
            });
        }

    }
}