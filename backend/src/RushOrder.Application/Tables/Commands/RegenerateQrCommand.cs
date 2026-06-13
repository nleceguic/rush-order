using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Tables.Commands;

public record RegenerateQrResult(string NewQrCode, string NewQrUrl);

public record RegenerateQrCommand(Guid TableId) : ICommand<RegenerateQrResult>;

public sealed class RegenerateQrCommandValidator : AbstractValidator<RegenerateQrCommand>
{
    public RegenerateQrCommandValidator()
    {
        RuleFor(x => x.TableId).NotEmpty();
    }
}

public sealed class RegenerateQrCommandHandler : IRequestHandler<RegenerateQrCommand, RegenerateQrResult>
{
    private readonly ITableRepository _tableRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQrCodeSettings _qrCodeSettings;

    public RegenerateQrCommandHandler(
        ITableRepository tableRepository,
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork,
        IQrCodeSettings qrCodeSettings)
    {
        _tableRepository = tableRepository;
        _restaurantRepository = restaurantRepository;
        _unitOfWork = unitOfWork;
        _qrCodeSettings = qrCodeSettings;
    }

    public async Task<RegenerateQrResult> Handle(RegenerateQrCommand request, CancellationToken cancellationToken)
    {
        var table = await _tableRepository.GetByIdAsync(request.TableId, cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.TableId);

        var restaurant = await _restaurantRepository.GetByIdAsync(table.RestaurantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Restaurant), table.RestaurantId);

        var baseUrl = _qrCodeSettings.BaseUrl;
        var unique = Guid.NewGuid().ToString("N");
        var hash = HMACSHA256.HashData(restaurant.Id.ToByteArray(), Encoding.UTF8.GetBytes(unique));
        var newQrCode = (unique[..8] + Convert.ToHexString(hash)[..8]).ToUpperInvariant();
        var newQrUrl = $"{baseUrl.TrimEnd('/')}/{newQrCode}";

        table.SetQrCode(newQrCode, newQrUrl);

        await _tableRepository.UpdateAsync(table, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegenerateQrResult(newQrCode, newQrUrl);
    }
}
