using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Onboarding.DTOs;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;
using RushOrder.Domain.ValueObjects;

namespace RushOrder.Application.Onboarding.Commands;

public sealed record RegisterTenantCommand(
    string BusinessName,
    string OwnerEmail,
    string OwnerPassword,
    string OwnerFirstName,
    string OwnerLastName,
    string? Phone = null) : ICommand<RegisterTenantResult>;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.OwnerPassword).NotEmpty().MinimumLength(8)
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.");
        RuleFor(x => x.OwnerFirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerLastName).NotEmpty().MaximumLength(100);
    }
}

public sealed class RegisterTenantCommandHandler : IRequestHandler<RegisterTenantCommand, RegisterTenantResult>
{
    private readonly ITenantRepository _tenantRepo;
    private readonly IUserRepository _userRepo;
    private readonly IRestaurantRepository _restaurantRepo;
    private readonly ITableRepository _tableRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IProductRepository _productRepo;
    private readonly IPlanRepository _planRepo;
    private readonly ISubscriptionRepository _subscriptionRepo;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly INotificationService _notifications;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterTenantCommandHandler(
        ITenantRepository tenantRepo,
        IUserRepository userRepo,
        IRestaurantRepository restaurantRepo,
        ITableRepository tableRepo,
        ICategoryRepository categoryRepo,
        IProductRepository productRepo,
        IPlanRepository planRepo,
        ISubscriptionRepository subscriptionRepo,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        INotificationService notifications,
        IUnitOfWork unitOfWork)
    {
        _tenantRepo = tenantRepo;
        _userRepo = userRepo;
        _restaurantRepo = restaurantRepo;
        _tableRepo = tableRepo;
        _categoryRepo = categoryRepo;
        _productRepo = productRepo;
        _planRepo = planRepo;
        _subscriptionRepo = subscriptionRepo;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegisterTenantResult> Handle(
        RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepo.ExistsAnyWithEmailAsync(request.OwnerEmail, cancellationToken))
            throw new BusinessRuleException($"An account with email '{request.OwnerEmail}' already exists.");

        var slug = GenerateSlug(request.BusinessName);
        if (await _tenantRepo.ExistsBySlugAsync(slug, cancellationToken))
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";

        var starterPlan = await _planRepo.GetStarterPlanAsync(cancellationToken)
            ?? throw new BusinessRuleException("No Starter plan available. Please contact support.");

        var trialEnd = DateTimeOffset.UtcNow.AddDays(30);

        // 1. Tenant
        var tenant = Tenant.Create(request.BusinessName, slug, starterPlan.Id, trialEnd);
        await _tenantRepo.AddAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var tenantId = tenant.Id;

        // 2. Subscription (trial)
        var subscription = Subscription.CreateTrial(tenantId, starterPlan.Id, trialEnd);
        await _subscriptionRepo.AddAsync(subscription, cancellationToken);

        // 3. Owner user
        var passwordHash = _passwordHasher.Hash(request.OwnerPassword);
        var owner = User.Create(tenantId, request.OwnerEmail, passwordHash,
            request.OwnerFirstName, request.OwnerLastName, UserRole.Owner);

        await _userRepo.AddAsync(owner, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 4. Restaurant
        var restaurant = Restaurant.Create(
            tenantId,
            name: request.BusinessName,
            address: "Por configurar",
            phone: request.Phone ?? "+34 000 000 000",
            email: request.OwnerEmail,
            taxRate: 0.10m,
            timezone: "Europe/Madrid",
            currency: "EUR");

        await _restaurantRepo.AddAsync(restaurant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var restaurantId = restaurant.Id;

        owner.AddRestaurant(restaurantId);

        // 5. Sample tables
        for (var i = 1; i <= 5; i++)
        {
            var table = Table.Create(tenantId, restaurantId, $"Mesa {i}", capacity: i <= 3 ? 2 : 4);
            await _tableRepo.AddAsync(table, cancellationToken);
        }

        // 6. Sample categories and products
        var menuCat = Category.Create(tenantId, restaurantId, "Menú del día", sortOrder: 1);
        var bebidasCat = Category.Create(tenantId, restaurantId, "Bebidas", sortOrder: 2);
        await _categoryRepo.AddAsync(menuCat, cancellationToken);
        await _categoryRepo.AddAsync(bebidasCat, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var sampleProducts = new[]
        {
            Product.Create(tenantId, restaurantId, menuCat.Id, "Ensalada mixta",    new Money(6.50m,  "EUR"), 5),
            Product.Create(tenantId, restaurantId, menuCat.Id, "Plato del día",     new Money(10.90m, "EUR"), 15),
            Product.Create(tenantId, restaurantId, menuCat.Id, "Postre del chef",   new Money(4.50m,  "EUR"), 10),
            Product.Create(tenantId, restaurantId, bebidasCat.Id, "Agua 50cl",      new Money(1.50m,  "EUR"), 1),
            Product.Create(tenantId, restaurantId, bebidasCat.Id, "Refresco",       new Money(2.20m,  "EUR"), 1),
            Product.Create(tenantId, restaurantId, bebidasCat.Id, "Cerveza",        new Money(2.80m,  "EUR"), 1),
        };

        foreach (var product in sampleProducts)
            await _productRepo.AddAsync(product, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 7. JWT
        var tokenResult = _jwtTokenService.GenerateAccessToken(owner, tenantId);

        // 8. Welcome email (fire-and-forget)
        _ = TrySendWelcomeAsync(request.OwnerEmail,
            $"{request.OwnerFirstName} {request.OwnerLastName}",
            cancellationToken);

        return new RegisterTenantResult(tenantId, restaurantId, tokenResult.Token, tokenResult.ExpiresAt);
    }

    private async Task TrySendWelcomeAsync(string email, string name, CancellationToken ct)
    {
        try
        {
            await _notifications.SendWelcomeEmailAsync(email, name, "https://app.rushorder.io/login", ct);
        }
        catch { /* best-effort */ }
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");

        return new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray())
            .Trim('-');
    }
}
