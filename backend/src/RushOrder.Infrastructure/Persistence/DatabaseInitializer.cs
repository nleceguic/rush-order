using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RushOrder.Infrastructure.Persistence.Seeders;

namespace RushOrder.Infrastructure.Persistence;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        ILogger<DatabaseInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database initialization starting (environment: {Env})", _environment.EnvironmentName);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // Apply any pending EF Core migrations
            var pending = await ctx.Database.GetPendingMigrationsAsync(cancellationToken);
            var pendingList = pending.ToList();

            if (pendingList.Count > 0)
            {
                _logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                    pendingList.Count, string.Join(", ", pendingList));
                await ctx.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("Database schema is up to date");
            }

            // Seed based on environment
            var seeder = new DatabaseSeeder();

            if (_environment.IsDevelopment())
            {
                _logger.LogInformation("Seeding development data");
                await seeder.SeedDevelopmentDataAsync(ctx, cancellationToken);
                _logger.LogInformation("Development seed complete");
            }
            else
            {
                _logger.LogInformation("Seeding production baseline data");
                await seeder.SeedProductionDataAsync(ctx, cancellationToken);
                _logger.LogInformation("Production seed complete");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed");
            throw;  // Prevent the app from starting with a broken database
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
