using System.Text;
using System.Threading.RateLimiting;
using DotNetEnv;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Walos.API.Middleware;
using Walos.API.Services;
using Walos.Application;
using Walos.Domain.Interfaces;
using Walos.Infrastructure;

// =============================================
// LOAD .env FILE
// =============================================
var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
if (!File.Exists(envPath))
    envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
    Env.Load(envPath);

// =============================================
// SERILOG BOOTSTRAP
// =============================================
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Map .env variables to Configuration
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")))
        builder.Configuration["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_SECRET")))
        builder.Configuration["Jwt:Secret"] = Environment.GetEnvironmentVariable("JWT_SECRET");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_EXPIRES_MINUTES")))
        builder.Configuration["Jwt:ExpiresInMinutes"] = Environment.GetEnvironmentVariable("JWT_EXPIRES_MINUTES");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_REFRESH_SECRET")))
        builder.Configuration["Jwt:RefreshSecret"] = Environment.GetEnvironmentVariable("JWT_REFRESH_SECRET");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT_REFRESH_EXPIRES_DAYS")))
        builder.Configuration["Jwt:RefreshExpiresInDays"] = Environment.GetEnvironmentVariable("JWT_REFRESH_EXPIRES_DAYS");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        builder.Configuration["OpenAI:ApiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_MODEL")))
        builder.Configuration["OpenAI:Model"] = Environment.GetEnvironmentVariable("OPENAI_MODEL");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_MAX_TOKENS")))
        builder.Configuration["OpenAI:MaxTokens"] = Environment.GetEnvironmentVariable("OPENAI_MAX_TOKENS");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_TEMPERATURE")))
        builder.Configuration["OpenAI:Temperature"] = Environment.GetEnvironmentVariable("OPENAI_TEMPERATURE");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CORS_ORIGINS")))
        builder.Configuration["Cors:Origins"] = Environment.GetEnvironmentVariable("CORS_ORIGINS");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RATE_LIMIT_WINDOW_MS")))
        builder.Configuration["RateLimiting:WindowMs"] = Environment.GetEnvironmentVariable("RATE_LIMIT_WINDOW_MS");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RATE_LIMIT_MAX_REQUESTS")))
        builder.Configuration["RateLimiting:MaxRequests"] = Environment.GetEnvironmentVariable("RATE_LIMIT_MAX_REQUESTS");
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PORT")))
        builder.Configuration["Port"] = Environment.GetEnvironmentVariable("PORT");

    // Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/combined-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 5)
        .WriteTo.File("logs/error-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error, retainedFileCountLimit: 5));

    // =============================================
    // SERVICES
    // =============================================

    // Controllers + JSON options
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    // FluentValidation
    builder.Services.AddFluentValidationAutoValidation();

    // Swagger/OpenAPI
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Walos API",
            Version = "v1",
            Description = "API para gestión comercial con asistencia de IA"
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Ingresa tu token JWT"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // JWT Authentication
    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("JWT Secret not configured");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                        context.Response.Headers.Append("Token-Expired", "true");
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var origins = (builder.Configuration["Cors:Origins"] ?? "*")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(origin => origin.TrimEnd('/'))
                .Where(origin => !string.IsNullOrWhiteSpace(origin))
                .ToArray();

            if (origins.Contains("*"))
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            else
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        });
    });

    // Rate Limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("api", limiter =>
        {
            limiter.PermitLimit = int.Parse(builder.Configuration["RateLimiting:MaxRequests"] ?? "100");
            limiter.Window = TimeSpan.FromMilliseconds(int.Parse(builder.Configuration["RateLimiting:WindowMs"] ?? "900000"));
        });
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // Response Compression
    builder.Services.AddResponseCompression();

    // Tenant Context (scoped per request)
    builder.Services.AddScoped<ITenantContext, TenantContext>();

    // Application & Infrastructure layers (DI)
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // =============================================
    // APP PIPELINE
    // =============================================
    var app = builder.Build();

    // Exception handling middleware
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Swagger (all environments for now)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Walos API v1"));
    }

    // Serilog request logging
    app.UseSerilogRequestLogging();

    // Security & performance
    app.UseResponseCompression();
    app.UseCors();
    app.UseRateLimiter();

    // Static files (product images, uploads)
    app.UseStaticFiles();

    // Auth
    app.UseAuthentication();
    app.UseAuthorization();

    // Tenant context (MUST be after auth so JWT claims are available)
    app.UseMiddleware<TenantContextMiddleware>();

    // Map controllers
    app.MapControllers();

    // =============================================
    // START
    // =============================================
    var port = builder.Configuration["Port"] ?? "3000";
    Log.Information("✓ Servidor iniciando en puerto {Port}", port);
    Log.Information("✓ Ambiente: {Env}", app.Environment.EnvironmentName);
    Log.Information("✓ API: http://localhost:{Port}/api/v1", port);
    Log.Information("✓ Health: http://localhost:{Port}/health", port);
    Log.Information("✓ Swagger: http://localhost:{Port}/swagger", port);

    app.Run($"http://0.0.0.0:{port}");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Error fatal iniciando servidor");
}
finally
{
    Log.CloseAndFlush();
}
