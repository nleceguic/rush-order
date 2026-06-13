using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Tables.Commands;

public record CreateTableResult(Guid TableId, string QrCode, string QrUrl);

public record CreateTableCommand(
    Guid RestaurantId,
    string Name,
    int Capacity,
    string? Zone,
    double? PositionX,
    double? PositionY) : ICommand<CreateTableResult>;

public sealed class CreateTableCommandValidator : AbstractValidator<CreateTableCommand>
{
    public CreateTableCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 50);
        RuleFor(x => x.Zone).MaximumLength(100).When(x => x.Zone is not null);
    }
}

public sealed class CreateTableCommandHandler : IRequestHandler<CreateTableCommand, CreateTableResult>
{
    private readonly ITableRepository _tableRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;
    private readonly IQrCodeSettings _qrCodeSettings;

    public CreateTableCommandHandler(
        ITableRepository tableRepository,
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService,
        IQrCodeSettings qrCodeSettings)
    {
        _tableRepository = tableRepository;
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
        _qrCodeSettings = qrCodeSettings;
    }

    public async Task<CreateTableResult> Handle(CreateTableCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var restaurant = await _restaurantRepository.GetByIdAsync(request.RestaurantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Restaurant), request.RestaurantId);

        var baseUrl = _qrCodeSettings.BaseUrl;
        var table = Table.Create(tenantId, restaurant.Id, request.Name, request.Capacity, request.Zone, baseUrl);

        var qrCode = GenerateQrCode(restaurant.Id);
        var qrUrl = $"{baseUrl.TrimEnd('/')}/{qrCode}";
        table.SetQrCode(qrCode, qrUrl);

        if (request.PositionX.HasValue && request.PositionY.HasValue)
            table.SetPosition(request.PositionX.Value, request.PositionY.Value);

        await _tableRepository.AddAsync(table, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateTableResult(table.Id, table.QrCode, table.QrUrl);
    }

    private static string GenerateQrCode(Guid restaurantId)
    {
        var unique = Guid.NewGuid().ToString("N");
        var hash = HMACSHA256.HashData(restaurantId.ToByteArray(), Encoding.UTF8.GetBytes(unique));
        return (unique[..8] + Convert.ToHexString(hash)[..8]).ToUpperInvariant();
    }
}
