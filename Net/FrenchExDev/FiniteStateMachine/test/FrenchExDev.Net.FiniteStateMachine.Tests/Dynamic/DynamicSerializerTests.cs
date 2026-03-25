using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Dynamic;

public class DynamicSerializerTests
{
    [Fact]
    public void ToJson_RoundTrips_Correctly()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("Created")
            .FinalState("Delivered")
            .When("Created")
                .On("Submit").TransitionTo("Submitted")
            .When("Submitted")
                .On("Ship").TransitionTo("Shipped")
            .When("Shipped")
                .On("Deliver").TransitionTo("Delivered")
            .Build()
            .Value!;

        var json = definition.ToJson();
        var restored = DynamicStateMachineSerializer.FromJson(json);

        Assert.Equal("Created", restored.InitialState);
        Assert.Contains("Delivered", restored.FinalStates);
        Assert.Equal(definition.Transitions.Count, restored.Transitions.Count);
    }

    [Fact]
    public async Task FromJson_ProducesWorkingMachine()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .When("A")
                .On("Go").TransitionTo("B")
            .When("B")
                .On("Back").TransitionTo("A")
            .Build()
            .Value!;

        var json = definition.ToJson();
        var restored = DynamicStateMachineSerializer.FromJson(json);
        var machine = restored.CreateMachine();

        var result = await machine.FireAsync("Go");
        Assert.True(result.IsSuccess);
        Assert.Equal("B", machine.CurrentState);

        result = await machine.FireAsync("Back");
        Assert.True(result.IsSuccess);
        Assert.Equal("A", machine.CurrentState);
    }

    [Fact]
    public void ToJson_ContainsExpectedFields()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("Start")
            .FinalState("End")
            .When("Start")
                .On("Finish").TransitionTo("End")
            .Build()
            .Value!;

        var json = definition.ToJson();

        Assert.Contains("\"initialState\"", json);
        Assert.Contains("\"Start\"", json);
        Assert.Contains("\"End\"", json);
        Assert.Contains("\"Finish\"", json);
    }
}
