namespace FrenchExDev.Net.Diem.Workflow.Scheduling;

public interface IScheduledTransitionService
{
    Task ProcessPendingTransitionsAsync(CancellationToken ct = default);
}
