using MediatR;
using RushOrder.Application.Common.Interfaces;

namespace RushOrder.Application.Common.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionBehavior(IUnitOfWork unitOfWork)
        => _unitOfWork = unitOfWork;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only wrap commands in an explicit DB transaction
        if (request is not IBaseCommand)
            return await next();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var response = await next();
            await _unitOfWork.CommitAsync();
            return response;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
