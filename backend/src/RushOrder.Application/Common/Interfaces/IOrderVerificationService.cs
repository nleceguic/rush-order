namespace RushOrder.Application.Common.Interfaces;

public interface IOrderVerificationService
{
    string GenerateToken(Guid orderId);
    bool VerifyToken(Guid orderId, string token);
}
