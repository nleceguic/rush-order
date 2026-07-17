using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Application.Experiments.DTOs;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Experiments.Queries;

public record GetExperimentAssignmentQuery(
    Guid RestaurantId, string ExperimentKey, string DeviceFingerprint) : IQuery<ExperimentAssignmentDto>;

public sealed class GetExperimentAssignmentQueryHandler
    : IRequestHandler<GetExperimentAssignmentQuery, ExperimentAssignmentDto>
{
    private readonly IRestaurantRepository _restaurants;
    private readonly IExperimentRepository _experiments;

    public GetExperimentAssignmentQueryHandler(IRestaurantRepository restaurants, IExperimentRepository experiments)
    {
        _restaurants = restaurants;
        _experiments = experiments;
    }

    public async Task<ExperimentAssignmentDto> Handle(GetExperimentAssignmentQuery request, CancellationToken cancellationToken)
    {
        var bucket = ExperimentBucketing.ComputeBucket(request.DeviceFingerprint);

        var restaurant = await _restaurants.GetByIdAsync(request.RestaurantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Restaurant), request.RestaurantId);

        var experiment = await _experiments.GetActiveByKeyAsync(
            restaurant.TenantId, request.RestaurantId, request.ExperimentKey, cancellationToken);

        // No Experiment row configured yet for this restaurant -> default to a
        // 50/50 split so "Recomendaciones en el carrito" works out of the box
        // without requiring an admin to create it first.
        var splitPercent = experiment?.VariantBSplitPercent ?? 50;
        var variant = bucket < splitPercent ? ExperimentVariant.B : ExperimentVariant.A;

        return new ExperimentAssignmentDto(request.ExperimentKey, variant.ToString(), bucket);
    }
}
