using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;
using RushOrder.Domain.Enums;

namespace RushOrder.Application.Experiments.Commands;

public record RecordExperimentEventCommand(
    Guid RestaurantId,
    string ExperimentKey,
    string Variant,
    string DeviceFingerprint,
    string EventType,
    Guid? OrderId,
    decimal? CartTotal) : ICommand<Unit>;

public sealed class RecordExperimentEventCommandValidator : AbstractValidator<RecordExperimentEventCommand>
{
    public RecordExperimentEventCommandValidator()
    {
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.ExperimentKey).NotEmpty();
        RuleFor(x => x.DeviceFingerprint).NotEmpty();
        RuleFor(x => x.Variant)
            .Must(v => Enum.TryParse<ExperimentVariant>(v, out _)).WithMessage("Variant must be 'A' or 'B'.");
        RuleFor(x => x.EventType)
            .Must(v => Enum.TryParse<ExperimentEventType>(v, out _))
            .WithMessage("EventType must be 'Exposure', 'SuggestionAdded' or 'OrderCompleted'.");
    }
}

public sealed class RecordExperimentEventCommandHandler : IRequestHandler<RecordExperimentEventCommand, Unit>
{
    private readonly IRestaurantRepository _restaurants;
    private readonly IExperimentRepository _experiments;

    public RecordExperimentEventCommandHandler(IRestaurantRepository restaurants, IExperimentRepository experiments)
    {
        _restaurants = restaurants;
        _experiments = experiments;
    }

    public async Task<Unit> Handle(RecordExperimentEventCommand request, CancellationToken cancellationToken)
    {
        var restaurant = await _restaurants.GetByIdAsync(request.RestaurantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Restaurant), request.RestaurantId);

        var result = ExperimentResult.Create(
            restaurant.TenantId,
            request.RestaurantId,
            request.ExperimentKey,
            Enum.Parse<ExperimentVariant>(request.Variant),
            request.DeviceFingerprint,
            Enum.Parse<ExperimentEventType>(request.EventType),
            request.OrderId,
            request.CartTotal);

        await _experiments.RecordEventAsync(result, cancellationToken);

        return Unit.Value;
    }
}
