using FrenchExDev.Net.FiniteStateMachine.Dynamic;
using FrenchExDev.Net.FiniteStateMachine.Tests.Engine.RichSimple;
using FrenchExDev.Net.FiniteStateMachine.Rich;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for StateMachineEngine: concurrency, listeners,
/// guard denial, internal transitions, hierarchy bubbling.
/// </summary>
public class FullCoverageTests_Engine
{
    // ═══════════════════════════════════════════════════════
    // StateMachineEngine — Semaphore concurrent fires, OnError listener, RemoveListener
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Engine_Semaphore_MultipleConcurrentFires_AllComplete()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A").On("Go").TransitionTo("B")
            .When("B").On("Back").TransitionTo("A")
            .Build().Value!;

        var machine = definition.CreateMachine(ConcurrencyMode.Semaphore);

        // Fire multiple transitions concurrently
        var tasks = new List<Task>();
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await machine.FireAsync("Go");
                await machine.FireAsync("Back");
            }));
        }

        await Task.WhenAll(tasks);

        // Machine should be in a valid state (either A or B)
        Assert.True(machine.CurrentState == "A" || machine.CurrentState == "B");
    }

    [Fact]
    public async Task Engine_RemoveListener_StopsNotifications()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A").On("Go").TransitionTo("B")
            .When("B").On("Back").TransitionTo("A")
            .Build().Value!;

        var machine = definition.CreateMachine();
        var listener = new CountingDynamicListener();
        machine.AddListener(listener);

        await machine.FireAsync("Go");
        Assert.Equal(1, listener.TransitionedCount);

        machine.RemoveListener(listener);
        await machine.FireAsync("Back");
        Assert.Equal(1, listener.TransitionedCount); // not incremented
    }

    [Fact]
    public async Task Engine_AllGuardsDeny_ReturnsDenialMessage()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
                    .WithGuard((f, e, t, ct) => Task.FromResult(false))
            .Build().Value!;

        var machine = definition.CreateMachine();
        var listener = new DeniedListener();
        machine.AddListener(listener);

        var result = await machine.FireAsync("Go");
        Assert.True(result.IsFailure);
        Assert.True(listener.DeniedCalled);
        Assert.Contains("guards denied", listener.Reason!);
    }

    [Fact]
    public async Task Engine_NoTransition_ReturnsDenialMessage()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A").On("Go").TransitionTo("B")
            .Build().Value!;

        var machine = definition.CreateMachine();
        var listener = new DeniedListener();
        machine.AddListener(listener);

        var result = await machine.FireAsync("Invalid");
        Assert.True(result.IsFailure);
        Assert.True(listener.DeniedCalled);
        Assert.Contains("No transition", listener.Reason!);
    }

    [Fact]
    public async Task Engine_GetPermittedEventsAsync_ReturnsCorrect()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
                .On("Stay").InternalTransition()
            .Build().Value!;

        var machine = definition.CreateMachine();
        var events = await machine.GetPermittedEventsAsync();
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task Engine_CanFireAsync_WithGuardFailing_ReturnsFalse()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
                    .WithGuard((f, e, t, ct) => Task.FromResult(false))
            .Build().Value!;

        var machine = definition.CreateMachine();
        Assert.False(await machine.CanFireAsync("Go"));
    }

    [Fact]
    public async Task Engine_CanFireAsync_NoTransition_ReturnsFalse()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A").On("Go").TransitionTo("B")
            .Build().Value!;

        var machine = definition.CreateMachine();
        Assert.False(await machine.CanFireAsync("Invalid"));
    }

    // ═══════════════════════════════════════════════════════
    // Engine — internal transition skips exit/entry but runs actions
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Engine_InternalTransition_SkipsEntryExitButRunsAction()
    {
        var actionRan = false;
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Ping").InternalTransition()
                    .WithAction((f, e, t, ct) => { actionRan = true; return Task.CompletedTask; })
            .Build().Value!;

        var machine = definition.CreateMachine();
        var listener = new ExitEntryTracker();
        machine.AddListener(listener);

        await machine.FireAsync("Ping");

        Assert.True(actionRan);
        Assert.False(listener.ExitCalled);
        Assert.False(listener.EnterCalled);
        Assert.Equal("A", machine.CurrentState);
    }

    // ═══════════════════════════════════════════════════════
    // Engine — Hierarchy property exposed
    // ═══════════════════════════════════════════════════════

    [Fact]
    public void Engine_Hierarchy_NullWhenNotProvided()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A").On("Go").TransitionTo("B")
            .Build().Value!;

        var machine = new StateMachineEngine<string, string>(definition);
        Assert.Null(machine.Hierarchy);
    }

    [Fact]
    public void Engine_Hierarchy_NotNullWhenProvided()
    {
        var hierarchy = new StateHierarchy<string>();
        hierarchy.AddChild("P", "C");

        var definition = new DynamicStateMachineBuilder()
            .InitialState("C")
            .When("C").On("Go").TransitionTo("Done")
            .Build().Value!;

        var machine = new StateMachineEngine<string, string>(definition, hierarchy: hierarchy);
        Assert.NotNull(machine.Hierarchy);
    }

    // ═══════════════════════════════════════════════════════
    // Engine: entry/exit actions with hierarchy (LCA-based exits)
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task Engine_WithHierarchy_FindsTransitionInParent_WhenChildHasNone()
    {
        var hierarchy = new StateHierarchy<string>();
        hierarchy.AddChild("Active", "Sub1", isInitial: true);
        hierarchy.AddChild("Active", "Sub2");

        var definition = new DynamicStateMachineBuilder()
            .InitialState("Sub1")
            .When("Active")
                .On("Cancel").TransitionTo("Done")
            .When("Sub1")
                .On("Next").TransitionTo("Sub2")
            .Build().Value!;

        var machine = new StateMachineEngine<string, string>(
            definition, hierarchy: hierarchy);

        // From Sub1, "Cancel" is on parent "Active" — should bubble up
        var result = await machine.FireAsync("Cancel");
        Assert.True(result.IsSuccess);
        Assert.Equal("Done", machine.CurrentState);
    }

    // ═══════════════════════════════════════════════════════
    // Rich FSM listener lifecycle — all listener methods called
    // ═══════════════════════════════════════════════════════

    [Fact]
    public async Task RichMachine_Fire_NotifiesAllListenerMethods()
    {
        var machine = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .When<StateA>()
                .On<GoEvent>()
                    .TransitionTo((e, s) => new StateB())
            .Build().Value!;

        var listener = new FullTrackingListener();
        machine.AddListener(listener);

        await machine.FireAsync(new GoEvent());

        Assert.Equal(1, listener.TransitioningCount);
        Assert.Equal(1, listener.TransitionedCount);
        Assert.Equal(1, listener.ExitingCount);
        Assert.Equal(1, listener.EnteredCount);
    }

    [Fact]
    public async Task RichMachine_FireDenied_NotifiesDeniedListener()
    {
        var machine = new RichStateMachineBuilder<ISimpleState, ISimpleEvent>()
            .InitialState(new StateA())
            .Build().Value!;

        var listener = new FullTrackingListener();
        machine.AddListener(listener);

        await machine.FireAsync(new GoEvent());

        Assert.Equal(1, listener.DeniedCount);
    }

    // ═══════════════════════════════════════════════════════
    // Helper types
    // ═══════════════════════════════════════════════════════

    private sealed class CountingDynamicListener : StateMachineListenerBase<string, string>
    {
        public int TransitionedCount { get; private set; }
        public override Task OnTransitionedAsync(string from, string @event, string to, CancellationToken ct)
        { TransitionedCount++; return Task.CompletedTask; }
    }

    private sealed class DeniedListener : StateMachineListenerBase<string, string>
    {
        public bool DeniedCalled { get; private set; }
        public string? Reason { get; private set; }
        public override Task OnTransitionDeniedAsync(string from, string @event, string reason, CancellationToken ct)
        {
            DeniedCalled = true;
            Reason = reason;
            return Task.CompletedTask;
        }
    }

    private sealed class ExitEntryTracker : StateMachineListenerBase<string, string>
    {
        public bool ExitCalled { get; private set; }
        public bool EnterCalled { get; private set; }
        public override Task OnStateExitingAsync(string state, string @event, CancellationToken ct)
        { ExitCalled = true; return Task.CompletedTask; }
        public override Task OnStateEnteringAsync(string state, string triggeringEvent, CancellationToken ct)
        { EnterCalled = true; return Task.CompletedTask; }
    }

    private sealed class FullTrackingListener : StateMachineListenerBase<ISimpleState, ISimpleEvent>
    {
        public int TransitioningCount { get; private set; }
        public int TransitionedCount { get; private set; }
        public int ExitingCount { get; private set; }
        public int EnteredCount { get; private set; }
        public int DeniedCount { get; private set; }

        public override Task OnTransitioningAsync(ISimpleState from, ISimpleEvent @event, ISimpleState to, CancellationToken ct)
        { TransitioningCount++; return Task.CompletedTask; }
        public override Task OnTransitionedAsync(ISimpleState from, ISimpleEvent @event, ISimpleState to, CancellationToken ct)
        { TransitionedCount++; return Task.CompletedTask; }
        public override Task OnStateExitingAsync(ISimpleState state, ISimpleEvent @event, CancellationToken ct)
        { ExitingCount++; return Task.CompletedTask; }
        public override Task OnStateEnteredAsync(ISimpleState state, ISimpleEvent triggeringEvent, CancellationToken ct)
        { EnteredCount++; return Task.CompletedTask; }
        public override Task OnTransitionDeniedAsync(ISimpleState from, ISimpleEvent @event, string reason, CancellationToken ct)
        { DeniedCount++; return Task.CompletedTask; }
    }
}
