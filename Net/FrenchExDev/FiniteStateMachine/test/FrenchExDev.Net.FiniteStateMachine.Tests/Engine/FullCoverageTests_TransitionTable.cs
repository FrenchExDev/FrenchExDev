using FrenchExDev.Net.FiniteStateMachine.Tests.Typed;
using FrenchExDev.Net.FiniteStateMachine.Typed;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// TransitionTable coverage tests: GetPermittedEvents empty, AllTransitions multiple, Find miss.
/// </summary>
public class FullCoverageTests_TransitionTable
{
    [Fact]
    public void TransitionTable_GetPermittedEvents_EmptyTable_ReturnsEmpty()
    {
        var table = new TransitionTable<DoorState, DoorEvent>();
        var events = table.GetPermittedEvents(DoorState.Closed);
        Assert.Empty(events);
    }

    [Fact]
    public void TransitionTable_GetPermittedEvents_MultipleTransitionsFromSameState()
    {
        var table = new TransitionTable<DoorState, DoorEvent>();
        table.Add(new TransitionDefinition<DoorState, DoorEvent>(
            DoorState.Closed, DoorEvent.Open, DoorState.Open,
            Array.Empty<IGuard<DoorState, DoorEvent>>(),
            Array.Empty<ITransitionAction<DoorState, DoorEvent>>()));
        table.Add(new TransitionDefinition<DoorState, DoorEvent>(
            DoorState.Closed, DoorEvent.Lock, DoorState.Locked,
            Array.Empty<IGuard<DoorState, DoorEvent>>(),
            Array.Empty<ITransitionAction<DoorState, DoorEvent>>()));

        var events = table.GetPermittedEvents(DoorState.Closed);
        Assert.Equal(2, events.Count);
        Assert.Contains(DoorEvent.Open, events);
        Assert.Contains(DoorEvent.Lock, events);
    }

    [Fact]
    public void TransitionTable_Find_NoMatch_ReturnsEmpty()
    {
        var table = new TransitionTable<DoorState, DoorEvent>();
        var result = table.Find(DoorState.Closed, DoorEvent.Open);
        Assert.Empty(result);
    }

    [Fact]
    public void TransitionTable_AllTransitions_EmptyTable_ReturnsNone()
    {
        var table = new TransitionTable<DoorState, DoorEvent>();
        Assert.Empty(table.AllTransitions.ToList());
    }

    [Fact]
    public void TransitionTable_AllTransitions_MultipleEntries_ReturnsAll()
    {
        var table = new TransitionTable<DoorState, DoorEvent>();
        table.Add(new TransitionDefinition<DoorState, DoorEvent>(
            DoorState.Closed, DoorEvent.Open, DoorState.Open,
            Array.Empty<IGuard<DoorState, DoorEvent>>(),
            Array.Empty<ITransitionAction<DoorState, DoorEvent>>()));
        table.Add(new TransitionDefinition<DoorState, DoorEvent>(
            DoorState.Open, DoorEvent.Close, DoorState.Closed,
            Array.Empty<IGuard<DoorState, DoorEvent>>(),
            Array.Empty<ITransitionAction<DoorState, DoorEvent>>()));
        // Add a second guard for the same state+event
        table.Add(new TransitionDefinition<DoorState, DoorEvent>(
            DoorState.Closed, DoorEvent.Open, DoorState.Locked,
            Array.Empty<IGuard<DoorState, DoorEvent>>(),
            Array.Empty<ITransitionAction<DoorState, DoorEvent>>()));

        var all = table.AllTransitions.ToList();
        Assert.Equal(3, all.Count);
    }
}
