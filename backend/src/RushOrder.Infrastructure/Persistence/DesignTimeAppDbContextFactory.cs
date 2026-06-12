using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Infrastructure.Persistence;

/// <summary>
/// Used only by EF Core tooling (dotnet ef migrations add/update).
/// Never instantiated at runtime.
/// </summary>
public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Try to read from the API project's appsettings (startup project dir)
        var config = new ConfigurationBuilder()
            .SetBasePath(ResolveApiDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config["Database:ConnectionString"]
            ?? "Host=localhost;Port=5432;Database=rushorder_dev;Username=rushorder;Password=rushorder_dev_pass";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options, new DesignTimeCurrentTenantService(), null!);
    }

    private static string ResolveApiDirectory()
    {
        // Walk up from Infrastructure project dir looking for the API project
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "backend", "src", "RushOrder.API");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private sealed class DesignTimeCurrentTenantService : ICurrentTenantService
    {
        public Guid? TenantId => null;
        public bool IsAuthenticated => false;
    }
}
