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
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace RushOrder.API.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
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

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        // Trigger host build now that containers are ready.
        // ConfigureWebHost overrides (test connection strings, DisableRateLimit, etc.) are applied
        // here. DatabaseInitializer is removed below so the host starts without auto-migration.
        _ = CreateClient();

        // Run migrations via EF Core
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Seed development data (plans + demo tenant + admin)
        var seeder = new DatabaseSeeder();
        await seeder.SeedDevelopmentDataAsync(db, CancellationToken.None);

        // Initialise Respawn after migrations so schema is known
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            // Keep plan rows between resets — they are seeded once and never mutated by tests
            TablesToIgnore = [new Respawn.Graph.Table("plans")]
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
                // Dummy Stripe keys so StripeOptions validation passes
                ["Stripe:SecretKey"]          = "sk_test_dummy",
                ["Stripe:PublishableKey"]     = "pk_test_dummy",
                ["Stripe:WebhookSecret"]      = TestConstants.StripeWebhookSecret,
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove DatabaseInitializer — migrations run manually in InitializeAsync after
            // containers are confirmed ready, so the host can start cleanly without it.
            foreach (var sd in services.Where(d => d.ImplementationType == typeof(DatabaseInitializer)).ToList())
                services.Remove(sd);

            // Replace real SMTP with a no-op implementation
            var notifDescriptor = services.Single(d => d.ServiceType == typeof(INotificationService));
            services.Remove(notifDescriptor);
            services.AddScoped<INotificationService, NullNotificationService>();

            // Replace real Stripe gateway with an in-memory fake
            var stripeDescriptor = services.Single(d => d.ServiceType == typeof(IStripeGateway));
            services.Remove(stripeDescriptor);
            services.AddScoped<IStripeGateway, FakeStripeGateway>();
        });
    }

    /// <summary>Resets the DB to a clean post-migration state and re-seeds.</summary>
    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(PostgresConnectionString);
        await conn.OpenAsync();
        await _respawner!.ResetAsync(conn);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = new DatabaseSeeder();
        await seeder.SeedDevelopmentDataAsync(db, CancellationToken.None);
    }

    public new async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        await _redis.StopAsync();
        await base.DisposeAsync();
    }
}
