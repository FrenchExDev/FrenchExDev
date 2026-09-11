namespace FrenchExDev.Net.Mediator;

/// <summary>
/// Marker interface for a request that returns <typeparamref name="TResult"/>.
/// </summary>
public interface IRequest<TResult> { }

/// <summary>
/// Marker interface for a command (write-side CQRS) that returns <typeparamref name="TResult"/>.
/// </summary>
public interface ICommand<TResult> : IRequest<TResult> { }

/// <summary>
/// Marker interface for a query (read-side CQRS) that returns <typeparamref name="TResult"/>.
/// </summary>
public interface IQuery<TResult> : IRequest<TResult> { }
