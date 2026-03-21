namespace FrenchExDev.Net.Ddd
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IQueryHandler<in TQuery, TResult>
    {
        Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
    }
}
