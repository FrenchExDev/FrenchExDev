using FrenchExDev.Net.FiniteStateMachine.Dynamic;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine;

/// <summary>
/// Coverage tests for DynamicStateMachineSerializer: invalid JSON, null deserialization.
/// </summary>
public class FullCoverageTests_Serializer
{
    [Fact]
    public void Serializer_FromJson_InvalidJson_Throws()
    {
        Assert.Throws<System.Text.Json.JsonException>(
            () => DynamicStateMachineSerializer.FromJson("not valid json"));
    }

    [Fact]
    public void Serializer_FromJson_NullDeserialized_Throws()
    {
        // JSON "null" deserializes to null FsmDto
        Assert.Throws<InvalidOperationException>(
            () => DynamicStateMachineSerializer.FromJson("null"));
    }

    [Fact]
    public void Serializer_FromJson_NullInitialState_ThrowsInvalidOperation()
    {
        // JSON with null initialState causes builder.Build() to fail,
        // which triggers the "Deserialized FSM definition is invalid" path
        var json = """{"initialState":null,"states":["A"],"finalStates":[],"transitions":[]}""";
        Assert.Throws<InvalidOperationException>(
            () => DynamicStateMachineSerializer.FromJson(json));
    }

    [Fact]
    public void Serializer_RoundTrip_WithFinalStates_PreservesDefinition()
    {
        var definition = new DynamicStateMachineBuilder()
            .InitialState("A")
            .FinalState("C")
            .When("A").On("Go").TransitionTo("B")
            .When("B").On("Go").TransitionTo("C")
            .Build().Value!;

        var json = definition.ToJson();
        var restored = DynamicStateMachineSerializer.FromJson(json);

        Assert.Equal("A", restored.InitialState);
        Assert.Contains("C", restored.FinalStates);
        Assert.Equal(2, restored.Transitions.Count);
    }
}
