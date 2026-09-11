namespace FrenchExDev.Net.Vos.Abstractions.Pipeline;

/// <summary>Handles a Vos pipeline request and produces a result.</summary>
public interface IVosHandler<in TRequest, TResult> where TRequest : IVosRequest<TResult> where TResult : notnull
{
    Task<Res.Result<TResult>> HandleAsync(TRequest request, CancellationToken ct = default);
}
