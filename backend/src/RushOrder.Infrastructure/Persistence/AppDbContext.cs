using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Common;
using RushOrder.Domain.Entities;

namespace RushOrder.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IUnitOfWork
{
    private readonly ICurrentTenantService _currentTenant;
    private readonly IMediator _mediator;
    private IDbContextTransaction? _currentTransaction;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentTenantService currentTenant,
        IMediator mediator) : base(options)
    {
        _currentTenant = currentTenant;
        _mediator = mediator;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global multi-tenant query filters on all TenantEntity subclasses
        modelBuilder.Entity<Restaurant>().HasQueryFilter(e => e.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Table>().HasQueryFilter(e => e.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<User>().HasQueryFilter(e => e.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Customer>().HasQueryFilter(e => e.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Order>().HasQueryFilter(e => e.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Product>().HasQueryFilter(e => e.TenantId == _currentTenant.TenantId);
        modelBuilder.Entity<Payment>().HasQueryFilter(e => e.TenantId == _currentTenant.TenantId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        await DispatchDomainEventsAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void SetAuditFields()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                // CreatedAt and Id are set in the constructor; only protect UpdatedAt
                entry.Property(nameof(BaseEntity.UpdatedAt)).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(BaseEntity.UpdatedAt)).CurrentValue = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            if (entry.State == EntityState.Added && _currentTenant.TenantId.HasValue)
            {
                // Only set TenantId if the entity hasn't already had it set via constructor
                var current = (Guid)entry.Property(nameof(TenantEntity.TenantId)).CurrentValue!;
                if (current == Guid.Empty)
                    entry.Property(nameof(TenantEntity.TenantId)).CurrentValue = _currentTenant.TenantId.Value;
            }
        }
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var entities = ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        entities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
            await _mediator.Publish(domainEvent, cancellationToken);
    }

    // IUnitOfWork
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
            throw new InvalidOperationException("A transaction is already in progress.");
        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
            throw new InvalidOperationException("No active transaction.");
        try
        {
            await SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null) return;
        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
}
