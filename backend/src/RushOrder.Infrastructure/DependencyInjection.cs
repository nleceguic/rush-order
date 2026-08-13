using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Forecasting;
using RushOrder.Infrastructure.Identity;
using RushOrder.Infrastructure.Interceptors;
using RushOrder.Infrastructure.Persistence;
using RushOrder.Infrastructure.Persistence.Repositories;
using RushOrder.Infrastructure.Services;
using RushOrder.Infrastructure.Settings;
using StackExchange.Redis;
using Stripe;

namespace RushOrder.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<TenantDbCommandInterceptor>();

        var connectionString = configuration.GetSection("Database")["ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is not configured.");

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.AddInterceptors(sp.GetRequiredService<TenantDbCommandInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ITableRepository, TableRepository>();
        services.AddScoped<IRestaurantRepository, RestaurantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();

        // ── Recommendations & A/B experiments ────────────────────────────────
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<IRecommendationService, RushOrder.Infrastructure.Services.RecommendationService>();
        services.AddScoped<IPairingRuleRepository, PairingRuleRepository>();
        services.AddScoped<IExperimentRepository, ExperimentRepository>();
        services.AddScoped<IOrderRatingRepository, OrderRatingRepository>();

        // ── Demand forecasting ────────────────────────────────────────────────
        services.AddScoped<IDemandForecastRepository, DemandForecastRepository>();
        services.AddScoped<DemandForecastEngine>();
        services.AddHttpClient<IHolidayProvider, NagerDateHolidayProvider>(client =>
        {
            client.BaseAddress = new Uri("https://date.nager.at/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHostedService<DemandForecastJob>();

        // ── Kitchen ETA prediction ───────────────────────────────────────────
        services.AddScoped<IPrepTimeRepository, PrepTimeRepository>();
        services.AddScoped<IPrepTimeService, PrepTimeService>();
        services.AddHostedService<KitchenLoadMonitorService>();

        // ── Weekly insights email (SendGrid) ─────────────────────────────────
        services.Configure<SendGridSettings>(configuration.GetSection("SendGrid"));
        services.AddScoped<IEmailSender, SendGridEmailService>();
        services.AddScoped<IWeeklyInsightsRepository, WeeklyInsightsRepository>();
        services.AddHostedService<WeeklyInsightsJob>();
        services.AddHostedService<MiseEnPlaceJob>();

        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<ISubscriptionService, RushOrder.Infrastructure.Services.SubscriptionService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
        services.AddScoped<INotificationService, SmtpNotificationService>();

        var qrCodeSettings = new QrCodeSettings();
        configuration.GetSection("QrCode").Bind(qrCodeSettings);
        services.AddSingleton<IQrCodeSettings>(qrCodeSettings);

        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
        services.AddStackExchangeRedisCache(options => options.Configuration = redisConnectionString);
        // Register IConnectionMultiplexer so health checks and OTel Redis instrumentation can use it
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddScoped<IMenuCacheService, RedisMenuCacheService>();
        services.AddSingleton<IQrCodeImageService, QrCodeImageService>();

        // ── Service Bus admin client (for health checks + pending-messages count) ──
        var sbConnStr = configuration["ServiceBus:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(sbConnStr))
            services.AddSingleton(new ServiceBusAdministrationClient(sbConnStr));

        // ── Analytics ─────────────────────────────────────────────────────────
        services.AddSingleton<IExcelExportService, ExcelExportService>();
        services.AddHostedService<AlertMonitoringService>();

        // ── Stripe ────────────────────────────────────────────────────────────
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<IPdfReceiptService, QuestPdfReceiptService>();

        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.Section));
        var stripeSecretKey = configuration[$"{StripeOptions.Section}:SecretKey"];
        if (!string.IsNullOrEmpty(stripeSecretKey))
            StripeConfiguration.ApiKey = stripeSecretKey;

        services.AddScoped<IStripeGateway, StripeGateway>();
        services.AddScoped<IStripeWebhookService, StripeWebhookService>();

        // ── JWT Settings ──────────────────────────────────────────────────────
        var jwtSection = configuration.GetSection("Jwt");
        var jwtSettings = jwtSection.Get<JwtSettings>() ?? new JwtSettings();
        if (!string.IsNullOrEmpty(configuration["Jwt:Issuer"]))
            jwtSettings.Issuer = configuration["Jwt:Issuer"]!;
        if (!string.IsNullOrEmpty(configuration["Jwt:Audience"]))
            jwtSettings.Audience = configuration["Jwt:Audience"]!;

        services.Configure<JwtSettings>(opts =>
        {
            opts.Issuer = jwtSettings.Issuer;
            opts.Audience = jwtSettings.Audience;
            opts.AccessTokenExpirationMinutes = jwtSettings.AccessTokenExpirationMinutes;
            opts.RefreshTokenExpirationDays = jwtSettings.RefreshTokenExpirationDays;
            opts.PrivateKeyPath = jwtSettings.PrivateKeyPath;
            opts.PublicKeyPath = jwtSettings.PublicKeyPath;
        });

        // ── RSA Key Provider (singleton — loads/creates .pem files) ──────────
        var rsaKeyProvider = new FileRsaKeyProvider(jwtSettings);
        services.AddSingleton<IRsaKeyProvider>(rsaKeyProvider);

        // ── Identity services ─────────────────────────────────────────────────
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IAuthCacheService, RedisAuthCacheService>();
        services.AddSingleton<IOrderVerificationService, OrderVerificationService>();

        // ── Repositories ──────────────────────────────────────────────────────
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // ── JWT Bearer Authentication ─────────────────────────────────────────
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Without this, the handler silently remaps "sub"/"email"/"tid" to legacy
                // XML/WS-Fed claim URIs on validation (e.g. "tid" -> the Azure AD tenant-id
                // claim URI). CurrentTenantService reads the literal "tid" claim with no
                // fallback, so every tenant-scoped EF query filter silently resolved to
                // TenantId == null (constant-folded by EF to `WHERE FALSE`) for every
                // authenticated request.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(rsaKeyProvider.GetPublicKey()),
                    ClockSkew = TimeSpan.Zero
                };
                options.Events = new JwtBearerEvents
                {
                    // SignalR WebSocket cannot send Authorization header — pick token from query string
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/ws"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var cache = context.HttpContext.RequestServices
                            .GetRequiredService<IAuthCacheService>();
                        var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                        if (jti is not null && await cache.IsJtiBlockedAsync(jti))
                            context.Fail("Token has been revoked.");
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
