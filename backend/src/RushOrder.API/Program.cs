using System.IO.Compression;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RushOrder.API.Filters;
using RushOrder.API.Middleware;
using RushOrder.API.Options;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Infrastructure;
using RushOrder.Infrastructure.Hubs;
using RushOrder.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

// ── Bootstrap logger (activo hasta que Serilog se configure con DI) ───────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── 1. SERILOG ────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .Enrich.WithProperty("Application", "RushOrder.API")
           .WriteTo.Console(
               outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");

        if (!ctx.HostingEnvironment.IsDevelopment())
        {
            cfg.WriteTo.File(
                path: "logs/rushorder-.log",
                rollingInterval: Serilog.RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
        }
    });

    // ── 2. OPCIONES FUERTEMENTE TIPADAS (validadas al arranque) ───────────────
    builder.Services
        .AddOptions<JwtOptions>()
        .BindConfiguration(JwtOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services
        .AddOptions<CorsPolicyOptions>()
        .BindConfiguration(CorsPolicyOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services
        .AddOptions<DatabaseOptions>()
        .BindConfiguration(DatabaseOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services
        .AddOptions<StripeApiOptions>()
        .BindConfiguration(StripeApiOptions.Section)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // ── 3. CORS ───────────────────────────────────────────────────────────────
    var corsConfig = builder.Configuration
        .GetSection(CorsPolicyOptions.SectionName)
        .Get<CorsPolicyOptions>() ?? new();

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
        {
            if (corsConfig.AllowedOrigins.Length > 0)
                policy.WithOrigins(corsConfig.AllowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            else
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
        }));

    // ── 4. RESPONSE COMPRESSION (Brotli + Gzip) ───────────────────────────────
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(
        opts => opts.Level = CompressionLevel.Optimal);
    builder.Services.Configure<GzipCompressionProviderOptions>(
        opts => opts.Level = CompressionLevel.SmallestSize);

    // ── 5. RATE LIMITING: 100 req/min por IP (política global) ───────────────
    var disableRateLimit = builder.Configuration["DisableRateLimit"] == "true";
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            if (disableRateLimit)
                return RateLimitPartition.GetNoLimiter("no-limit");

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        options.OnRejected = async (context, token) =>
        {
            await context.HttpContext.Response.WriteAsJsonAsync(
                new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Title  = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Rate limit exceeded. Please retry after one minute."
                }, token);
        };
    });

    // ── 6. PROBLEM DETAILS (RFC 7807) + GLOBAL EXCEPTION HANDLER ─────────────
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // ── 7. INFRASTRUCTURE (EF Core, repositories, tenant service) ───────────
    builder.Services.AddInfrastructure(builder.Configuration);

    // ── 8. MEDIATR ────────────────────────────────────────────────────────────
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblies(
            typeof(Program).Assembly,
            typeof(ICurrentTenantService).Assembly));   // Application assembly

    // ── 9. FLUENT VALIDATION ──────────────────────────────────────────────────
    builder.Services.AddValidatorsFromAssemblies(
    [
        typeof(Program).Assembly,
        typeof(ICurrentTenantService).Assembly          // Application assembly
    ]);

    // ── 10. CONTROLLERS + AUDIT FILTER ───────────────────────────────────────
    builder.Services.AddControllers(options =>
        options.Filters.Add<AuditActionFilter>())
        .AddJsonOptions(opts =>
            opts.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter()));

    // ── 11. SIGNALR + REDIS BACKPLANE ─────────────────────────────────────────
    var signalRRedis = builder.Configuration["Redis:ConnectionString"];
    var signalR = builder.Services.AddSignalR(opts =>
    {
        opts.EnableDetailedErrors = builder.Environment.IsDevelopment();
        opts.KeepAliveInterval = TimeSpan.FromSeconds(15);
        opts.HandshakeTimeout = TimeSpan.FromSeconds(30);
        opts.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    });

    if (!string.IsNullOrWhiteSpace(signalRRedis))
        signalR.AddStackExchangeRedis(signalRRedis, opts =>
            opts.Configuration.ChannelPrefix = new StackExchange.Redis.RedisChannel(
                "rushorder", StackExchange.Redis.RedisChannel.PatternMode.Literal));

    // ── 12. DATABASE INITIALIZER (migrations + seeding on startup) ────────────
    builder.Services.AddHostedService<DatabaseInitializer>();

    // ── 13. OPENAPI / SWAGGER ────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new() { Title = "Rush Order API", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "JWT Bearer token",
            Name = "Authorization",
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "bearer"
        });
        options.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc),
                new List<string>()
            }
        });
    });

    // ── 12. HEALTH CHECKS ─────────────────────────────────────────────────────
    var dbConfig = builder.Configuration
        .GetSection(DatabaseOptions.SectionName)
        .Get<DatabaseOptions>() ?? new();
    var redisConn = builder.Configuration["Redis:ConnectionString"] ?? string.Empty;

    var healthChecks = builder.Services.AddHealthChecks();

    if (!string.IsNullOrWhiteSpace(dbConfig.ConnectionString))
        healthChecks.AddNpgSql(dbConfig.ConnectionString, name: "postgres", tags: ["ready", "db"]);

    if (!string.IsNullOrWhiteSpace(redisConn))
        healthChecks.AddRedis(redisConn, name: "redis", tags: ["ready", "cache"]);

    healthChecks.AddCheck("disk", () =>
    {
        var drive = DriveInfo.GetDrives()
            .FirstOrDefault(d => d.IsReady && (d.Name == "/" || d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase)));

        if (drive is null)
            return HealthCheckResult.Degraded("No suitable drive found");

        var freePct = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
        return freePct > 5
            ? HealthCheckResult.Healthy($"Disk OK: {freePct:F1}% free ({drive.Name})")
            : HealthCheckResult.Degraded($"Low disk space: {freePct:F1}% free ({drive.Name})");
    }, tags: ["ready"]);

    // ──────────────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── PIPELINE DE MIDDLEWARES (orden importa) ───────────────────────────────

    app.UseExceptionHandler();           // 1. Captura excepciones no controladas
    app.UseHttpsRedirection();           // 2. Redirige HTTP → HTTPS
    app.UseResponseCompression();        // 3. Comprime respuestas (Brotli/Gzip)
    app.UseRateLimiter();                // 4. Rate limiting global
    app.UseCors();                       // 5. CORS

    app.UseSerilogRequestLogging(opts => // 6. Log estructurado de requests
    {
        opts.EnrichDiagnosticContext = (diag, http) =>
        {
            diag.Set("UserId", http.User.FindFirst("sub")?.Value ?? "anonymous");
            diag.Set("RequestHost", http.Request.Host.Value);
            diag.Set("UserAgent", http.Request.Headers.UserAgent.ToString());
        };
    });

    app.UseAuthentication();             // 7. Autenticación
    app.UseAuthorization();              // 8. Autorización
    app.UseMiddleware<RushOrder.API.Middleware.PlanLimitsMiddleware>(); // 9. Plan limits

    if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Staging")
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Rush Order API v1"));
    }

    // ── HEALTH CHECK ENDPOINTS ────────────────────────────────────────────────
    var jsonOpts = new JsonSerializerOptions { WriteIndented = true };

    // /health — checks completos (db + redis + disco)
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (ctx, report) =>
        {
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status         = report.Status.ToString(),
                totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
                checks         = report.Entries.Select(e => new
                {
                    name        = e.Key,
                    status      = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs  = Math.Round(e.Value.Duration.TotalMilliseconds, 2),
                    tags        = e.Value.Tags,
                    error       = e.Value.Exception?.Message
                })
            }, jsonOpts));
        }
    });

    // /health/ready — liveness check (solo responde 200/503, sin checks externos)
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate             = _ => false,
        AllowCachingResponses = false
    });

    app.MapControllers();

    app.MapHub<RestaurantHub>("/ws/restaurant");
    app.MapHub<OrderTrackingHub>("/ws/order-tracking");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
