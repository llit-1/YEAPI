using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using YEAPI.Hubs;
using YEAPI.Middleware;
using YEAPI.Models;
using YEAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();
// SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<CashConnectionManager>();
builder.Services.AddSingleton<CashSignalRService>();
builder.Services.AddSingleton<RK7Service>();
// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введите JWT токен"
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] =
                new List<string>()
        });
});

// Database
builder.Services.AddDbContext<YEContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("RKNET")));

// ======================================================
// JWT Authentication
// ======================================================

var signingKey = builder.Configuration["Jwt:SigningKey"];

if (string.IsNullOrWhiteSpace(signingKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is not configured.");
}

var key = Encoding.UTF8.GetBytes(signingKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                // JWT должен быть выпущен нашей API
                ValidateIssuer = true,
                ValidIssuer = "YEAPI",

                // Все клиенты работают с одной API
                ValidateAudience = true,
                ValidAudience = "YEAPI",

                // Проверяем срок действия
                ValidateLifetime = true,

                // Проверяем подпись
                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(key),

                // После exp токен сразу недействителен
                ClockSkew = TimeSpan.Zero
            };
    });

// Authorization
builder.Services.AddAuthorization();
//Services
builder.Services.AddHttpClient<YandexEatsService>();

// ФГИС доступен только с российских адресов —
// ходим напрямую, мимо системного прокси/VPN
builder.Services.AddHttpClient<FsaService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseProxy = false
    });

builder.Services.AddScoped<GlobalFunctionService>();

builder.Services.AddHostedService<StopUpdateService>();
builder.Services.AddHostedService<PriceUpdateService>();
builder.Services.AddHostedService<YandexSalesService>();
builder.Services.AddHostedService<RestaurantBlockLogService>();

// Отправка стоп-листа в Яндекс с задержкой 20 сек по точке
builder.Services.AddSingleton<YandexStopSyncBuffer>();
builder.Services.AddHostedService<YandexStopSyncService>();

var app = builder.Build();



app.UseHttpsRedirection();

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();

app.UseMiddleware<HttpLoggingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<CashHub>("/hubs/cash");

app.Run();