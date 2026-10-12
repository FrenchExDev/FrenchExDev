using FrenchExDev.Net.FiniteStateMachine.Tests.SourceGenerator.RichSg;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.SourceGenerator;

public class RichSgTests
{
    [Fact]
    public void Visitor_IsGenerated_AndWorks()
    {
        var state = new OnState(75);

        var result = state.Accept<string>(new LightStateNameVisitor());

        Assert.Equal("On:75", result);
    }

    [Fact]
    public void Match_IsGenerated_AndWorks()
    {
        ILightState state = new OnState(100);

        var label = state.Match(
            idleState: _ => "idle",
            onState: s => $"on:{s.Brightness}",
            brokenState: s => $"broken:{s.Reason}");

        Assert.Equal("on:100", label);
    }

    [Fact]
    public void Match_Void_IsGenerated_AndWorks()
    {
        ILightState state = new BrokenState("overheated");
        string? captured = null;

        state.Match(
            idleState: _ => { },
            onState: _ => { },
            brokenState: s => { captured = s.Reason; });

        Assert.Equal("overheated", captured);
    }

    [Fact]
    public void Match_AllStatesExhaustive()
    {
        // This test verifies that Match covers all states — if a new state is added
        // without updating the Match call, this won't compile.
        var states = new ILightState[]
        {
            new IdleState(),
            new OnState(50),
            new BrokenState("test")
        };

        foreach (var state in states)
        {
            var name = state.Match(
                idleState: _ => "Idle",
                onState: _ => "On",
                brokenState: _ => "Broken");

            Assert.NotNull(name);
        }
    }

    [Fact]
    public void EventAccept_IsGenerated()
    {
        var evt = new TurnOnEvent(80);

        var result = evt.Accept<string>(new LightEventNameVisitor());

        Assert.Equal("TurnOn:80", result);
    }

    // Visitor implementation to test Accept
    private sealed class LightStateNameVisitor : ILightStateVisitor<string>
    {
        public string Visit(IdleState state) => "Idle";
        public string Visit(OnState state) => $"On:{state.Brightness}";
        public string Visit(BrokenState state) => $"Broken:{state.Reason}";
    }

    private sealed class LightEventNameVisitor : ILightEventVisitor<string>
    {
        public string Visit(TurnOnEvent @event) => $"TurnOn:{@event.Brightness}";
        public string Visit(TurnOffEvent @event) => "TurnOff";
        public string Visit(BreakEvent @event) => $"Break:{@event.Reason}";
    }
}
