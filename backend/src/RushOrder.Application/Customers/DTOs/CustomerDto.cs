namespace RushOrder.Application.Customers.DTOs;

public record CustomerDto(
    Guid Id,
    string DisplayName,
    string? Email,
    string? Phone,
    string? FirstName,
    string? LastName,
    int LoyaltyPoints,
    string LoyaltyLevel,
    DateTimeOffset CreatedAt);
