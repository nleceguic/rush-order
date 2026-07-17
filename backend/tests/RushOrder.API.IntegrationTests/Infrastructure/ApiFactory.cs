using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Infrastructure.Persistence;
using RushOrder.Infrastructure.Persistence.Seeders;
using RushOrder.Infrastructure.Services;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace RushOrder.API.IntegrationTests.Infrastructure;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("rushorder_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private Respawner? _respawner;

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public NotificationCapture NotificationCapture => Services.GetRequiredService<NotificationCapture>();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _ = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var seeder = new DatabaseSeeder();
        await seeder.SeedDevelopmentDataAsync(db, CancellationToken.None);

        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter         = DbAdapter.Postgres,
            SchemasToInclude  = ["public"],
            TablesToIgnore    = [new Respawn.Graph.Table("plans")]
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = _postgres.GetConnectionString(),
                ["Redis:ConnectionString"]    = _redis.GetConnectionString(),
                ["DisableRateLimit"]          = "true",
                ["Stripe:SecretKey"]          = "sk_test_dummy",
                ["Stripe:PublishableKey"]     = "pk_test_dummy",
                ["Stripe:WebhookSecret"]      = TestConstants.StripeWebhookSecret,
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove DatabaseInitializer — migrations run manually in InitializeAsync
            foreach (var sd in services.Where(d => d.ImplementationType == typeof(DatabaseInitializer)).ToList())
                services.Remove(sd);

            // Remove AlertMonitoringService — polls Postgres every 5 min, not needed in tests
            foreach (var sd in services.Where(d => d.ImplementationType == typeof(AlertMonitoringService)).ToList())
                services.Remove(sd);

            // Replace real notifications with a capturing implementation
            services.AddSingleton<NotificationCapture>();
            var notifDescriptor = services.Single(d => d.ServiceType == typeof(INotificationService));
            services.Remove(notifDescriptor);
            services.AddScoped<INotificationService, CapturingNotificationService>();

            // Replace real Stripe gateway with an in-memory fake
            var stripeDescriptor = services.Single(d => d.ServiceType == typeof(IStripeGateway));
            services.Remove(stripeDescriptor);
            services.AddScoped<IStripeGateway, FakeStripeGateway>();

            // Replace real TOTP service with a fixed-code fake for deterministic MFA tests
            var totpDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ITotpService));
            if (totpDescriptor is not null) services.Remove(totpDescriptor);
            services.AddScoped<ITotpService, FixedCodeTotpService>();
        });
    }

    /// <summary>Resets the DB to a clean post-migration state, re-seeds, and clears notification counters.</summary>
    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();
        await _respawner!.ResetAsync(conn);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = new DatabaseSeeder();
        await seeder.SeedDevelopmentDataAsync(db, CancellationToken.None);

        NotificationCapture.Reset();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        await _redis.StopAsync();
        await base.DisposeAsync();
    }
}
