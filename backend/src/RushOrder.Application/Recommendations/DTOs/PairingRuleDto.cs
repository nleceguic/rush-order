namespace RushOrder.Application.Recommendations.DTOs;

public sealed record PairingRuleDto(
    Guid Id,
    Guid SourceProductId,
    string SourceProductName,
    Guid TargetProductId,
    string TargetProductName,
    bool IsActive);
