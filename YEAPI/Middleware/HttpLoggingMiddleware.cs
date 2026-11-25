using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YEAPI.Attributes;
using YEAPI.Models;

namespace YEAPI.Middleware
{
    public class HttpLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _scopeFactory;

        public HttpLoggingMiddleware(
            RequestDelegate next,
            IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _scopeFactory = scopeFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Проверяем атрибут
            var endpoint = context.GetEndpoint();

            var shouldLog =
                endpoint?.Metadata.GetMetadata<HttpLogAttribute>() != null;

            if (!shouldLog)
            {
                await _next(context);
                return;
            }

            var datetimeBegin = DateTime.Now;

            string body = string.Empty;

            // ==========================================
            // Читаем Body запроса
            // ==========================================

            if (context.Request.ContentLength > 0)
            {
                context.Request.EnableBuffering();

                using var reader = new StreamReader(
                    context.Request.Body,
                    Encoding.UTF8,
                    leaveOpen: true);

                body = await reader.ReadToEndAsync();

                context.Request.Body.Position = 0;
            }

            // ==========================================
            // Получаем номер заказа из eatsId
            // ==========================================

            string? order = null;

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var json = JsonDocument.Parse(body);
                    var root = json.RootElement;

                    if (root.TryGetProperty("eatsId", out var property) ||
                        root.TryGetProperty("eats_id", out property))
                    {
                        order = property.GetString();
                    }
                }
                catch (JsonException)
                {
                    // Body не является JSON
                }
            }

            // Из URL: /order/{orderId}/...
            if (string.IsNullOrWhiteSpace(order))
            {
                order = context.Request.RouteValues["orderId"]?.ToString();
            }

            int statusCode = 0;
            string? errorText = null;
            Exception? exception = null;

            // ==========================================
            // Перехватываем Response
            // ==========================================

            var originalResponseBody = context.Response.Body;

            await using var responseBody = new MemoryStream();

            context.Response.Body = responseBody;

            try
            {
                await _next(context);

                statusCode = context.Response.StatusCode;

                // Читаем тело ответа
                context.Response.Body.Position = 0;

                using var responseReader = new StreamReader(
                    context.Response.Body,
                    Encoding.UTF8,
                    leaveOpen: true);

                var responseText = await responseReader.ReadToEndAsync();

                // Ошибки HTTP записываем в ErrorText
                if (statusCode >= 400)
                {
                    errorText = responseText;

                    if (string.IsNullOrWhiteSpace(errorText) &&
                        statusCode == 401)
                    {
                        errorText = "{\"type\":\"Unauthorized\"}";
                    }
                }

                // Возвращаем Response клиенту
                context.Response.Body.Position = 0;

                await responseBody.CopyToAsync(originalResponseBody);
            }
            catch (Exception ex)
            {
                statusCode = 500;
                errorText = ex.ToString();

                // Запоминаем исключение.
                // throw здесь НЕ делаем.
                exception = ex;
            }
            finally
            {
                context.Response.Body = originalResponseBody;
            }

            var datetimeEnd = DateTime.Now;

            // ==========================================
            // Собираем лог
            // ==========================================

            var log = new YEHttpsLog
            {
                ClientId = context.User.FindFirst("client_id")?.Value,

                IpAddress = context.Connection.RemoteIpAddress?.ToString(),

                Type = context.Request.Method,

                Url = $"{context.Request.Path}{context.Request.QueryString}",

                Body = body,

                DatetimeBegin = datetimeBegin,

                DatetimeEnd = datetimeEnd,

                StatusCode = statusCode,

                ErrorText = errorText,

                Order = order
            };

            // ==========================================
            // Записываем лог в фоне
            // ==========================================

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var db = scope.ServiceProvider
                        .GetRequiredService<YEContext>();

                    db.YEHttpsLog.Add(log);

                    await db.SaveChangesAsync();
                }
                catch
                {
                    // Ошибка логирования не должна влиять на API
                }
            });

            // ==========================================
            // Если было исключение — пробрасываем его дальше
            // ==========================================

            if (exception != null)
            {
                throw exception;
            }
        }
    }
}