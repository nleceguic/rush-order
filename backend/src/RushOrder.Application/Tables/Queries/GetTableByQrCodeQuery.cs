using MediatR;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Tables.DTOs;

namespace RushOrder.Application.Tables.Queries;

public record GetTableByQrCodeQuery(string QrCode) : IQuery<TablePublicDto?>;

public sealed class GetTableByQrCodeQueryHandler : IRequestHandler<GetTableByQrCodeQuery, TablePublicDto?>
{
    private readonly ITableRepository _tableRepository;
    private readonly IRestaurantRepository _restaurantRepository;

    public GetTableByQrCodeQueryHandler(ITableRepository tableRepository, IRestaurantRepository restaurantRepository)
    {
        _tableRepository = tableRepository;
        _restaurantRepository = restaurantRepository;
    }

    public async Task<TablePublicDto?> Handle(GetTableByQrCodeQuery request, CancellationToken cancellationToken)
    {
        var table = await _tableRepository.GetByQrCodeAsync(request.QrCode, cancellationToken);
        if (table is null) return null;

        var restaurant = await _restaurantRepository.GetByIdPublicAsync(table.RestaurantId, cancellationToken);
        if (restaurant is null || !restaurant.IsActive) return null;

        return new TablePublicDto(
            table.Id,
            table.Name,
            table.Capacity,
            table.Zone,
            restaurant.Id,
            restaurant.Name,
            restaurant.Currency,
            restaurant.LogoUrl,
            restaurant.CoverUrl,
            restaurant.Settings.UpsellingEnabled,
            AvailableLocales: ["es"]);
    }
}
