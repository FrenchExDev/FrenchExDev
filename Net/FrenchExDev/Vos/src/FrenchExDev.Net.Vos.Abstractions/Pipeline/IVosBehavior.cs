namespace FrenchExDev.Net.Vos.Abstractions.Pipeline;

/// <summary>Middleware around a handler. Cross-cutting concerns (logging, validation, locking, etc.).</summary>
public interface IVosBehavior<in TRequest, TResult> where TRequest : IVosRequest<TResult> where TResult : notnull
{
    Task<Res.Result<TResult>> HandleAsync(TRequest request, Func<Task<Res.Result<TResult>>> next, CancellationToken ct = default);
}
