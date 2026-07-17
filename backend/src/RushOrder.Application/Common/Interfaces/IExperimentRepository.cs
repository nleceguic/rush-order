using RushOrder.Application.Experiments.DTOs;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Common.Interfaces;

public interface IExperimentRepository
{
    Task<Experiment?> GetActiveByKeyAsync(Guid tenantId, Guid restaurantId, string key, CancellationToken cancellationToken = default);
    Task RecordEventAsync(ExperimentResult result, CancellationToken cancellationToken = default);
    Task<ExperimentResultsDto> GetResultsAsync(Guid tenantId, Guid restaurantId, string key, CancellationToken cancellationToken = default);
}
