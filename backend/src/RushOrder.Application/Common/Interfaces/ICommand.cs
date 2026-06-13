using MediatR;

namespace RushOrder.Application.Common.Interfaces;

public interface IBaseCommand { }

public interface ICommand<TResponse> : IRequest<TResponse>, IBaseCommand { }

public interface IQuery<TResponse> : IRequest<TResponse> { }
