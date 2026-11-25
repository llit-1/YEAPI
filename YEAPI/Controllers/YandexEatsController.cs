using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YEAPI.Attributes;
using YEAPI.Models;
using YEAPI.Services;

namespace YEAPI.Controllers
{
    [ApiController]
    [Route("yandex")]
    public class YandexEatsController : ControllerBase
    {
        private readonly YandexEatsService _yandex;
        private readonly GlobalFunctionService _globalFunctions;
        private readonly YEContext _db;

        public YandexEatsController(
            YandexEatsService yandex,
            GlobalFunctionService globalFunctions,
            YEContext db)
        {
            _yandex = yandex;
            _globalFunctions = globalFunctions;
            _db = db;
        }

        // ==========================================================
        // BLOCK
        // ==========================================================

        [HttpPost("block")]
        public async Task<IActionResult> BlockRestaurant(
            [FromBody] BlockRestaurantRequest model)
        {
            var response = await _yandex.BlockRestaurantAsync(
                model.LocationGuid,
                model.From,
                model.To,
                model.Message,
                "technical_problem");

            return await CopyResponse(response);
        }

        // ==========================================================
        // BLOCK STATUS
        // ==========================================================

        [HttpPost("block/status")]
        public async Task<IActionResult> GetBlockStatus(
            [FromBody] BlockStatusRequest model)
        {
            var response = await _yandex.GetBlockStatusAsync(
                model.LocationGuid,
                model.Statuses,
                model.Limit,
                model.Offset);

            return await CopyResponse(response);
        }

        // ==========================================================
        // UNBLOCK
        // ==========================================================

        [HttpPost("unblock")]
        public async Task<IActionResult> UnblockRestaurant(
            [FromBody] UnblockRestaurantRequest model)
        {
            var response = await _yandex.UnblockRestaurantAsync(
                model.LocationGuid,
                model.BlockIds);

            return await CopyResponse(response);
        }

        // ==========================================================
        // MENU IMPORT
        // ==========================================================

        [HttpPost("menu/import")]
        public async Task<IActionResult> MenuImport(
            [FromBody] MenuImportRequest model)
        {
            var response = await _yandex.InitiateMenuImportAsync(
                model.LocationGuid,
                model.OperationType);

            return await CopyResponse(response);
        }

        // ==========================================================
        // ORDERS HISTORY
        // ==========================================================

        [HttpPost("orders/history")]
        public async Task<IActionResult> OrdersHistory(
            [FromBody] OrdersHistoryRequest model)
        {
            var response = await _yandex.GetOrdersHistoryAsync(
                model.LocationGuid,
                model.From,
                model.To,
                model.Statuses,
                model.Limit,
                model.Offset);

            return await CopyResponse(response);
        }

        // ==========================================================
        // ORDER DETAILS
        // ==========================================================

        [HttpPost("orders/details")]
        public async Task<IActionResult> OrdersDetails(
            [FromBody] OrdersDetailsRequest model)
        {
            var response = await _yandex.GetOrdersDetailsAsync(
                model.EatsIds);

            return await CopyResponse(response);
        }

        // ==========================================================
        // SALES LOAD
        //
        // Ручная загрузка продаж за день (как ночной YandexSalesService)
        // ==========================================================

        [HttpPost("sales/load")]
        public async Task<IActionResult> SalesLoad(
            [FromBody] SalesLoadRequest model)
        {
            var begin = model.Date.Date;
            var end = begin.AddDays(1);

            var errors = new List<string>();

            var load = await _globalFunctions.LoadYandexSalesAsync(
                begin,
                end,
                errors);

            return Ok(new
            {
                loaded = load.Loaded,
                loadedOrders = load.LoadedOrders,
                skippedOrders = load.SkippedOrders,
                errors
            });
        }

        // ==========================================================
        // Копируем ответ Яндекса клиенту
        // ==========================================================

        private static async Task<IActionResult> CopyResponse(
            HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString()
                    ?? "application/json"
            };
        }
    }


    // ==============================================================
    // REQUEST MODELS
    // ==============================================================

    public class BlockRestaurantRequest
    {
        public Guid LocationGuid { get; set; }

        public DateTime From { get; set; }

        public DateTime? To { get; set; }

        public string Message { get; set; } = null!;
    }


    public class BlockStatusRequest
    {
        public Guid LocationGuid { get; set; }

        public List<string>? Statuses { get; set; }

        public int Limit { get; set; } = 100;

        public int Offset { get; set; } = 0;
    }


    public class UnblockRestaurantRequest
    {
        public Guid LocationGuid { get; set; }

        public List<string> BlockIds { get; set; } = new();
    }


    public class MenuImportRequest
    {
        public Guid LocationGuid { get; set; }

        public string OperationType { get; set; } = "menu";
    }


    public class OrdersHistoryRequest
    {
        public Guid LocationGuid { get; set; }

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public List<string>? Statuses { get; set; }

        public int Limit { get; set; } = 1000;

        public int Offset { get; set; } = 0;
    }


    public class OrdersDetailsRequest
    {
        public List<string> EatsIds { get; set; } = new();
    }


    public class SalesLoadRequest
    {
        public DateTime Date { get; set; }
    }
}