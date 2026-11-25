using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using YEAPI.Models;

namespace YEAPI.Controllers;

[ApiController]
[Route("security/oauth")]
public class AuthController : ControllerBase
{
    private readonly YEContext _db;
    private readonly IConfiguration _config;

    public AuthController(
        YEContext db,
        IConfiguration config)
    {
        _db = db;
        _config = config;
    }


    // ============================================================
    // СОЗДАНИЕ API-КЛИЕНТА
    // POST /security/oauth/client
    // ============================================================

    [AllowAnonymous]
    [HttpPost("client")]
    public async Task<IActionResult> CreateClient(
        [FromBody] CreateApiClientRequest request)
    {
        // Проверяем ClientId
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            return BadRequest(new
            {
                code = 400,
                description = "ClientId is required"
            });
        }

        // Проверяем ClientSecret
        if (string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            return BadRequest(new
            {
                code = 400,
                description = "ClientSecret is required"
            });
        }

        // Проверяем, что ClientId ещё не используется
        var exists = await _db.ApiClients
            .AnyAsync(x => x.ClientId == request.ClientId);

        if (exists)
        {
            return Conflict(new
            {
                code = 409,
                description = "ClientId already exists"
            });
        }

        // Создаём клиента
        var client = new ApiClient
        {
            Name = request.Name,
            ClientId = request.ClientId,

            // В БД сохраняем НЕ сам пароль,
            // а только BCrypt-хэш
            ClientSecret = BCrypt.Net.BCrypt.HashPassword(
                request.ClientSecret),

            Active = true
        };

        _db.ApiClients.Add(client);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = client.Id,
            name = client.Name,
            client_id = client.ClientId,
            active = client.Active
        });
    }


    // ============================================================
    // ПОЛУЧЕНИЕ JWT
    // POST /security/oauth/token
    // ============================================================

    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<IActionResult> Token(
        [FromForm] TokenRequest request)
    {
        // ==================================================
        // Проверяем grant_type
        // ==================================================

        if (request.grant_type != "client_credentials")
        {
            return BadRequest(new[]
            {
                new
                {
                    code = 400,
                    description = "Unsupported grant_type"
                }
            });
        }

        // ==================================================
        // Ищем клиента
        // ==================================================

        var client = await _db.ApiClients
            .FirstOrDefaultAsync(x =>
                x.ClientId == request.client_id &&
                x.Active);

        if (client == null)
        {
            return BadRequest(new[]
            {
                new
                {
                    code = 401,
                    description =
                        "Invalid client_id or client_secret"
                }
            });
        }

        // ==================================================
        // Проверяем client_secret
        // ==================================================

        bool secretValid;

        try
        {
            secretValid = BCrypt.Net.BCrypt.Verify(
                request.client_secret,
                client.ClientSecret);
        }
        catch
        {
            secretValid = false;
        }

        if (!secretValid)
        {
            return BadRequest(new[]
            {
                new
                {
                    code = 401,
                    description =
                        "Invalid client_id or client_secret"
                }
            });
        }

        // ==================================================
        // Lifetime
        // ==================================================

        if (!int.TryParse(
                _config["Jwt:LifetimeMinutes"],
                out var lifetimeMinutes))
        {
            throw new InvalidOperationException(
                "Jwt:LifetimeMinutes is not configured.");
        }

        var expires = DateTime.UtcNow.AddMinutes(
            lifetimeMinutes);

        // ==================================================
        // Signing key
        // ==================================================

        var signingKey = _config["Jwt:SigningKey"];

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured.");
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(signingKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        // ==================================================
        // Создаём JWT
        // ==================================================

        var token = new JwtSecurityToken(
            issuer: "YEAPI",

            // Одна общая аудитория для всей API
            audience: "YEAPI",

            claims:
            [
                // Кто получил этот токен
                new Claim(
                    "client_id",
                    client.ClientId)
            ],

            expires: expires,

            signingCredentials: credentials
        );

        var tokenString =
            new JwtSecurityTokenHandler()
                .WriteToken(token);

        // ==================================================
        // Ответ
        // ==================================================

        return Ok(new
        {
            access_token = tokenString,
            token_type = "Bearer",
            expires_in = (int)TimeSpan
                .FromMinutes(lifetimeMinutes)
                .TotalSeconds
        });
    }
}


// ============================================================
// DTO для создания клиента
// ============================================================

public class CreateApiClientRequest
{
    public string Name { get; set; } = "";

    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";
}
