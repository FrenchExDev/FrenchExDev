using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

public class TimerTests
{
    [Fact]
    public async Task TimerTransition_FiresAfterDuration()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("Waiting")
            .FinalState("TimedOut")
            .When("Waiting")
                .On("Timeout").TransitionTo("TimedOut")
            .Build().Value!;

        var machine = definition.CreateMachine();

        using var timer = new TimerTransition<string, string>(machine, "Timeout", TimeSpan.FromMilliseconds(50));
        timer.Start();

        // Wait for timer to fire
        await Task.Delay(200);

        Assert.Equal("TimedOut", machine.CurrentState);
    }

    [Fact]
    public async Task TimerTransition_CancelledBeforeFiring_DoesNotTransition()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("Waiting")
            .FinalState("TimedOut")
            .When("Waiting")
                .On("Timeout").TransitionTo("TimedOut")
            .Build().Value!;

        var machine = definition.CreateMachine();

        using var timer = new TimerTransition<string, string>(machine, "Timeout", TimeSpan.FromMilliseconds(500));
        timer.Start();

        // Cancel immediately
        timer.Cancel();

        await Task.Delay(100);

        Assert.Equal("Waiting", machine.CurrentState);
    }
}
