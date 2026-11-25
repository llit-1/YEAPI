using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YEAPI.Models;

namespace YEAPI.Services
{
    // ==============================================================
    // ФГИС Росаккредитации (pub.fsa.gov.ru)
    //
    // Реестр деклараций о соответствии. API недокументированное —
    // повторяем запросы, которые делает сам сайт:
    // 1. POST /login                                  — гостевой вход (anonymous)
    // 2. POST /api/v1/rds/common/declarations/get     — поиск по номеру
    // 3. GET  /api/v1/rds/common/declarations/{id}    — карточка
    // 4. POST /nsi/api/multi                          — расшифровка кодов ТН ВЭД
    // ==============================================================

    public class FsaService
    {
        // Без браузерного User-Agent сайт отвечает 403
        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

        // 1 — место нахождения, 3 — место осуществления деятельности
        private const int LegalAddressType = 1;
        private const int ProductionAddressType = 3;

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        private readonly YEContext _db;
        private readonly HttpClient _httpClient;

        public FsaService(
            YEContext db,
            HttpClient httpClient)
        {
            _db = db;
            _httpClient = httpClient;
        }

        // ==========================================================
        // Поиск декларации по номеру и разбор нужных полей
        //
        // null — декларация не найдена
        // ==========================================================

        public async Task<DeclarationLookupResponse?> LookupDeclarationAsync(
            string docNumber)
        {
            var fsaId = await FindDeclarationIdAsync(docNumber);

            if (fsaId == null)
                return null;

            var declaration = await GetDeclarationAsync(fsaId.Value);

            var identifications = declaration.Product?.Identifications
                ?? new List<FsaProductIdentification>();

            // Коды ТН ВЭД в карточке — это id справочника
            var tnvedIds = identifications
                .SelectMany(x => x.IdTnveds)
                .Distinct()
                .ToList();

            var tnveds = await GetTnvedsAsync(tnvedIds);

            var productName = string.Join("; ", identifications
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            return new DeclarationLookupResponse
            {
                DocNumber = declaration.Number ?? docNumber,

                FsaId = declaration.IdDeclaration,

                DateFrom = declaration.DeclRegDate,

                DateTo = declaration.DeclEndDate,

                ManufacturerName = declaration.Manufacturer?.FullName,

                LegalAddress = declaration.Manufacturer?.Addresses
                    .FirstOrDefault(x => x.IdAddrType == LegalAddressType)?
                    .FullAddress,

                ProductionAddress = JoinAddresses(declaration.ManufacturerFilials
                    .SelectMany(x => x.Addresses)
                    .Where(x => x.IdAddrType == ProductionAddressType)),

                ProductName = string.IsNullOrWhiteSpace(productName)
                    ? declaration.Product?.FullName
                    : productName,

                // Порядок — как в карточке
                TnvedCodes = tnvedIds
                    .Select(id => tnveds.FirstOrDefault(x => x.Id == id))
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Code))
                    .Select(x => new DeclarationTnvedDto
                    {
                        Code = x!.Code!,
                        Name = x.Name
                    })
                    .ToList()
            };
        }

        // ==========================================================
        // Поиск id декларации по номеру
        // ==========================================================

        private async Task<int?> FindDeclarationIdAsync(string docNumber)
        {
            var search = ToSearchNumber(docNumber);

            var requestBody = new
            {
                size = 100,
                page = 0,
                count = 0,
                filter = new
                {
                    status = Array.Empty<int>(),
                    idDeclType = Array.Empty<int>(),
                    idCertObjectType = Array.Empty<int>(),
                    idProductType = Array.Empty<int>(),
                    idGroupRU = Array.Empty<int>(),
                    idGroupEEU = Array.Empty<int>(),
                    idTechReg = Array.Empty<int>(),
                    idApplicantType = Array.Empty<int>(),
                    regDate = new { minDate = (string?)null, maxDate = (string?)null },
                    endDate = new { minDate = (string?)null, maxDate = (string?)null },
                    columnsSearch = new[]
                    {
                        new { name = "number", search, type = 0 }
                    },
                    number = search,
                    idProductOrigin = Array.Empty<int>(),
                    idProductEEU = Array.Empty<int>(),
                    idProductRU = Array.Empty<int>(),
                    idDeclScheme = Array.Empty<int>()
                },
                columnsSort = new[]
                {
                    new { column = "declDate", sort = "DESC" }
                }
            };

            var result = await SendAsync<FsaDeclarationSearchResponse>(
                HttpMethod.Post,
                "/api/v1/rds/common/declarations/get",
                requestBody);

            // Поиск по вхождению — может вернуть несколько деклараций.
            // Берём ту, чей номер совпадает с искомым.
            var key = ToCompareKey(docNumber);

            return result.Items
                .FirstOrDefault(x =>
                    x.Number != null &&
                    ToCompareKey(x.Number) == key)?
                .Id;
        }

        // ==========================================================
        // Карточка декларации
        // ==========================================================

        private Task<FsaDeclaration> GetDeclarationAsync(int id)
        {
            return SendAsync<FsaDeclaration>(
                HttpMethod.Get,
                $"/api/v1/rds/common/declarations/{id}");
        }

        // ==========================================================
        // Расшифровка id справочника ТН ВЭД в коды
        // ==========================================================

        private async Task<List<FsaTnved>> GetTnvedsAsync(List<int> ids)
        {
            if (ids.Count == 0)
                return new List<FsaTnved>();

            var requestBody = new
            {
                items = new
                {
                    tnved = new[]
                    {
                        new
                        {
                            id = ids,
                            fields = new[] { "id", "masterId", "name", "code", "hidden" }
                        }
                    }
                }
            };

            var result = await SendAsync<FsaNsiMultiResponse>(
                HttpMethod.Post,
                "/nsi/api/multi",
                requestBody);

            return result.Tnved;
        }

        // ==========================================================
        // Универсальный запрос к ФГИС
        //
        // - токен гостя
        // - повтор запроса при 401/403 с новым токеном
        // ==========================================================

        private async Task<T> SendAsync<T>(
            HttpMethod method,
            string path,
            object? body = null)
        {
            var settings = await GetSettingsAsync();

            var token = await GetAccessTokenAsync(settings);

            var response = await _httpClient.SendAsync(
                CreateRequest(method, settings.FsaHost!, path, token, body));

            // Токен недействителен — входим заново и повторяем
            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                response.Dispose();

                settings.FsaAccessToken = null;
                settings.FsaAccessTokenExpires = null;

                await _db.SaveChangesAsync();

                token = await GetAccessTokenAsync(settings);

                response = await _httpClient.SendAsync(
                    CreateRequest(method, settings.FsaHost!, path, token, body));
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"ФГИС вернул {(int)response.StatusCode} на {path}.");
                }

                var json = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<T>(json, JsonOptions)
                    ?? throw new InvalidOperationException(
                        $"ФГИС вернул пустой ответ на {path}.");
            }
        }

        private static HttpRequestMessage CreateRequest(
            HttpMethod method,
            string host,
            string path,
            string? token,
            object? body)
        {
            var request = new HttpRequestMessage(
                method,
                $"{host.TrimEnd('/')}{path}");

            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("application/json, text/plain, */*");

            if (token != null)
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            if (body != null)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json");
            }

            return request;
        }

        // ==========================================================
        // Токен гостя
        //
        // Сайт сам входит под anonymous и получает токен
        // в заголовке ответа Authorization. Токен живёт 8 часов.
        // ==========================================================

        private async Task<string> GetAccessTokenAsync(YESettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.FsaAccessToken) &&
                settings.FsaAccessTokenExpires.HasValue &&
                settings.FsaAccessTokenExpires.Value > DateTime.UtcNow)
            {
                return settings.FsaAccessToken;
            }

            if (string.IsNullOrWhiteSpace(settings.FsaLogin) ||
                string.IsNullOrWhiteSpace(settings.FsaPassword))
            {
                throw new InvalidOperationException(
                    "В YESettings не заполнены FsaLogin / FsaPassword.");
            }

            using var request = CreateRequest(
                HttpMethod.Post,
                settings.FsaHost!,
                "/login",
                null,
                new
                {
                    username = settings.FsaLogin,
                    password = settings.FsaPassword
                });

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"ФГИС: вход не выполнен ({(int)response.StatusCode}).");
            }

            if (!response.Headers.TryGetValues("Authorization", out var values))
            {
                throw new InvalidOperationException(
                    "ФГИС не вернул токен в заголовке Authorization.");
            }

            var token = values.First()
                .Replace("Bearer", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            settings.FsaAccessToken = token;

            // Срок берём из токена, с запасом 5 минут
            settings.FsaAccessTokenExpires =
                (GetTokenExpires(token) ?? DateTime.UtcNow.AddHours(8))
                .AddMinutes(-5);

            await _db.SaveChangesAsync();

            return token;
        }

        // exp из JWT (UTC)
        private static DateTime? GetTokenExpires(string token)
        {
            try
            {
                var payload = token.Split('.')[1]
                    .Replace('-', '+')
                    .Replace('_', '/');

                payload = payload.PadRight(
                    payload.Length + (4 - payload.Length % 4) % 4, '=');

                using var json = JsonDocument.Parse(
                    Convert.FromBase64String(payload));

                if (json.RootElement.TryGetProperty("exp", out var exp))
                    return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()).UtcDateTime;
            }
            catch
            {
                // Токен не JWT — используем срок по умолчанию
            }

            return null;
        }

        private async Task<YESettings> GetSettingsAsync()
        {
            var settings = await _db.YESettings
                .SingleAsync(x => x.Id == 1);

            if (string.IsNullOrWhiteSpace(settings.FsaHost))
            {
                throw new InvalidOperationException(
                    "В YESettings не заполнен FsaHost.");
            }

            return settings;
        }

        // ==========================================================
        // Номер декларации
        //
        // В наших перечнях номера записаны с лишними пробелами
        // и латиницей вместо кириллицы: "EAЭС N RU Д-RU. РА 02.В. 11576/26"
        // ==========================================================

        // Латинские буквы, похожие на кириллические
        private static readonly Dictionary<char, char> LatinToCyrillic = new()
        {
            ['A'] = 'А', ['B'] = 'В', ['C'] = 'С', ['E'] = 'Е', ['H'] = 'Н',
            ['K'] = 'К', ['M'] = 'М', ['O'] = 'О', ['P'] = 'Р', ['T'] = 'Т',
            ['X'] = 'Х', ['Y'] = 'У'
        };

        // Номер для поиска во ФГИС: "ЕАЭС N RU Д-RU.РА02.В.11576/26"
        private static string ToSearchNumber(string docNumber)
        {
            var compact = Regex.Replace(docNumber, @"\s+", "").ToUpperInvariant();

            var match = Regex.Match(compact, @"Д-RU\.?(.+)$");

            if (!match.Success)
                return docNumber.Trim();

            var tail = ReplaceLatin(match.Groups[1].Value);

            return $"ЕАЭС N RU Д-RU.{tail}";
        }

        // Ключ для сравнения номеров: без пробелов, латиница → кириллица
        private static string ToCompareKey(string docNumber)
        {
            var compact = Regex.Replace(docNumber, @"\s+", "").ToUpperInvariant();

            return ReplaceLatin(compact);
        }

        private static string ReplaceLatin(string value)
        {
            var chars = value.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (LatinToCyrillic.TryGetValue(chars[i], out var cyrillic))
                    chars[i] = cyrillic;
            }

            return new string(chars);
        }

        private static string? JoinAddresses(IEnumerable<FsaAddress> addresses)
        {
            var result = string.Join("; ", addresses
                .Select(x => x.FullAddress?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct());

            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
    }
}
