using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using YEAPI.Models;

namespace YEAPI.Services
{
    public class YandexEatsService
    {                                           
        private const string deliveryType = "native";

        // Тип доставки для блокировок (block / unblock / v2/status)
        private const string blockDeliveryType = "ddc";
        private readonly YEContext _db;
        private readonly HttpClient _httpClient;

        public YandexEatsService(
            YEContext db,
            HttpClient httpClient)
        {
            _db = db;
            _httpClient = httpClient;
        }

        // ==========================================================
        // Получение access token
        // ==========================================================

        public async Task<string> GetAccessTokenAsync()
        {
            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            // Если токен ещё действителен — возвращаем его
            if (!string.IsNullOrWhiteSpace(settings.YandexAccessToken) &&
                settings.YandexAccessTokenExpires.HasValue &&
                settings.YandexAccessTokenExpires.Value > DateTime.UtcNow)
            {
                return settings.YandexAccessToken;
            }

            // Получаем новый токен
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{settings.YandexHost}/oauth2/token");

            request.Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = settings.YandexClientId,
                    ["client_secret"] = settings.YandexClientSecret
                });

            using var response = await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            var token = JsonSerializer.Deserialize<YandexTokenResponse>(json);

            if (token == null ||
                string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException(
                    "Яндекс не вернул access_token.");
            }

            settings.YandexAccessToken = token.AccessToken;

            settings.YandexAccessTokenExpires =
                DateTime.UtcNow.AddSeconds(token.ExpiresIn);

            await _db.SaveChangesAsync();

            return token.AccessToken;
        }


        // ==========================================================
        // Универсальный запрос в Yandex Eats
        //
        // Все Push-методы используют его.
        // Здесь:
        // - access token
        // - Partner-Name
        // - JSON
        // - повтор запроса при 401
        // ==========================================================

        private async Task<HttpResponseMessage> SendAsync<T>(
            HttpMethod method,
            string url,
            T? body = default)
        {
            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            // ------------------------------------------------------
            // Первая попытка
            // ------------------------------------------------------

            var token = await GetAccessTokenAsync();

            using var request = CreateRequest(
                method,
                url,
                token,
                settings.YandexClientId,
                body);

            var response = await _httpClient.SendAsync(request);

            // ------------------------------------------------------
            // Если токен недействителен
            // ------------------------------------------------------

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();

                // Удаляем старый токен
                settings.YandexAccessToken = null;
                settings.YandexAccessTokenExpires = null;

                await _db.SaveChangesAsync();

                // Получаем новый токен
                token = await GetAccessTokenAsync();

                // --------------------------------------------------
                // Повторный запрос
                // --------------------------------------------------

                using var retryRequest = CreateRequest(
                    method,
                    url,
                    token,
                    settings.YandexClientId,
                    body);

                return await _httpClient.SendAsync(retryRequest);
            }

            return response;
        }


        // ==========================================================
        // Создание HTTP-запроса
        // ==========================================================

        private static HttpRequestMessage CreateRequest<T>(
            HttpMethod method,
            string url,
            string token,
            string clientId,
            T? body)
        {
            var request = new HttpRequestMessage(
                method,
                url);

            // Авторизация
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);

            // Partner-Name = ClientId
            request.Headers.Add(
                "Partner-Name",
                clientId);

            // JSON
            if (body != null)
            {
                var json = JsonSerializer.Serialize(body,new JsonSerializerOptions{ Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});

                request.Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");
            }

            return request;
        }


        // ==========================================================
        // BLOCK
        //
        // Блокировка ресторана
        // ==========================================================

        public async Task<HttpResponseMessage> BlockRestaurantAsync(
            Guid locationGuid,
            DateTime from,
            DateTime? to,
            string message,
            string tag)
        {
            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            var requestBody = new YandexBlockRequest
            {
                VendorInfo =
                [
                    new YandexVendorInfo
                    {
                        VendorId = locationGuid.ToString().ToUpperInvariant(),
                        DeliveryType = blockDeliveryType
                    }
                ],

                From = from.ToString("yyyy-MM-dd'T'HH:mm:sszzz"),

                To = to.HasValue
                    ? to.Value.ToString("yyyy-MM-dd'T'HH:mm:sszzz")
                    : null,

                Message = message,

                Tag = tag
            };

            return await SendAsync(
                HttpMethod.Post,
                $"{settings.YandexHost}/block",
                requestBody);
        }


        public async Task<HttpResponseMessage> GetBlockStatusAsync(Guid locationGuid, List<string>? statuses = null, int limit = 100, int offset = 0)
        {
            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            var requestBody = new YandexBlockStatusRequest
            {
                VendorInfo =
                [
                    new YandexVendorInfo
            {
                VendorId = locationGuid.ToString().ToUpperInvariant(),
                DeliveryType = blockDeliveryType
            }
                ],

                Statuses = statuses,

                Limit = limit,

                Offset = offset
            };

            return await SendAsync(
                HttpMethod.Post,
                $"{settings.YandexHost}/v2/status",
                requestBody);
        }

        // ==========================================================
        // UNBLOCK
        //
        // Разблокировка ресторана
        // ==========================================================

        public async Task<HttpResponseMessage> UnblockRestaurantAsync(
       Guid locationGuid,
       List<string> blockIds)
        {
            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            var requestBody = new YandexUnblockRequest
            {
                VendorInfo =
                [
                    new YandexVendorInfo
            {
                VendorId = locationGuid.ToString().ToUpperInvariant(),
                DeliveryType = blockDeliveryType
            }
                ],

                BlockIds = blockIds
            };

            return await SendAsync(
                HttpMethod.Post,
                $"{settings.YandexHost}/unblock",
                requestBody);
        }

        // ==========================================================
        // MENU IMPORT
        //
        // Принудительный запуск обновления меню / стоп-листа
        // ==========================================================

        public async Task<HttpResponseMessage> InitiateMenuImportAsync(
            Guid locationGuid,
            string operationType = "menu")
        {
            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            if (operationType != "menu" &&
                operationType != "menu_stop_list")
            {
                throw new ArgumentException(
                    "OperationType должен быть 'menu' или 'menu_stop_list'.",
                    nameof(operationType));
            }

            var requestBody = new YandexMenuImportRequest
            {
                RestaurantId = locationGuid.ToString().ToUpperInvariant(),
                OperationType = operationType
            };

            return await SendAsync(
                HttpMethod.Post,
                $"{settings.YandexHost}/menu/import/initiation",
                requestBody);
        }

        // ==========================================================
        // ORDERS HISTORY
        //
        // Получение истории заказов
        // ==========================================================

        public async Task<HttpResponseMessage> GetOrdersHistoryAsync(
            Guid locationGuid,
            DateTime from,
            DateTime to,
            List<string>? statuses = null,
            int limit = 1000,
            int offset = 0)
        {
            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            if (limit < 0 || limit > 1000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(limit),
                    "Limit должен быть от 0 до 1000.");
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    "Offset не может быть отрицательным.");
            }

            var requestBody = new YandexOrdersHistoryRequest
            {
                OriginIds =
                [
                    new YandexOriginId
            {
                OriginId = locationGuid.ToString().ToUpperInvariant(),
                DeliveryType = deliveryType
            }
                ],

                Service =
                [
                    "YE",
                    "MD"
                ],

                From = from.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"),

                To = to.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"),

                // Яндекс отвечает 400 на "status": null — передаём пустой список
                Status = statuses ?? new List<string>(),

                Pagination = new YandexPagination
                {
                    Limit = limit,
                    Offset = offset
                }
            };

            return await SendAsync(
                HttpMethod.Post,
                $"{settings.YandexHost}/v1/orders-history",
                requestBody);
        }

        // ==========================================================
        // ORDER DETAILS
        //
        // Получение детальной информации по заказам
        // ==========================================================

        public async Task<HttpResponseMessage> GetOrdersDetailsAsync(
            List<string> eatsIds)
        {
            if (eatsIds == null || eatsIds.Count == 0)
            {
                throw new ArgumentException(
                    "Необходимо передать хотя бы один eatsId.",
                    nameof(eatsIds));
            }

            if (eatsIds.Count > 100)
            {
                throw new ArgumentException(
                    "За один запрос можно передать не более 100 eatsId.",
                    nameof(eatsIds));
            }

            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            var requestBody = new YandexOrderDetailsRequest
            {
                EatsIds = eatsIds
            };

            return await SendAsync(
                HttpMethod.Post,
                $"{settings.YandexHost}/v1/get-orders-details",
                requestBody);
        }

        // ==========================================================
        // Ответ авторизации
        // ==========================================================

        private class YandexTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = null!;

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("scope")]
            public string Scope { get; set; } = null!;

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; } = null!;
        }
    }
}