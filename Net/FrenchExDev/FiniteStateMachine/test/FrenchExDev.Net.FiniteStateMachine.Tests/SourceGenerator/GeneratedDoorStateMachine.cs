using FrenchExDev.Net.FiniteStateMachine.Attributes;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.SourceGenerator;

public enum SgDoorState { Closed, Open, Locked }
public enum SgDoorEvent { Open, Close, Lock, Unlock }

[StateMachine(typeof(SgDoorState), typeof(SgDoorEvent), InitialState = nameof(SgDoorState.Closed))]
public partial class DoorStateMachine
{
    [Transition(SgDoorState.Closed, SgDoorEvent.Open, SgDoorState.Open)]
    [Transition(SgDoorState.Open, SgDoorEvent.Close, SgDoorState.Closed)]
    [Transition(SgDoorState.Closed, SgDoorEvent.Lock, SgDoorState.Locked)]
    [Transition(SgDoorState.Locked, SgDoorEvent.Unlock, SgDoorState.Closed)]
    private static partial void DefineTransitions();
}
