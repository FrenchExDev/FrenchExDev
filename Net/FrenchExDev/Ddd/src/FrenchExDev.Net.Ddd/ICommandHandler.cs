namespace FrenchExDev.Net.Ddd
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICommandHandler<in TCommand, TResult>
    {
        Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
    }
}
