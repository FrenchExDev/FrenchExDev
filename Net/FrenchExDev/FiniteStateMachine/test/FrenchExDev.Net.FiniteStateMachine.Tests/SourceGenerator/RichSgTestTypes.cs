using FrenchExDev.Net.FiniteStateMachine.Attributes;
using FrenchExDev.Net.FiniteStateMachine.Rich;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.SourceGenerator.RichSg;

// State interface
[RichStateMachine(InitialState = typeof(IdleState))]
public partial interface ILightState : IState { }

// Event interface
[RichStateMachineEvents(StateMachine = typeof(ILightState))]
public partial interface ILightEvent : IEvent { }

// States
[State]
public partial record IdleState() : ILightState { public string Name => "Idle"; }

[State]
public partial record OnState(int Brightness) : ILightState { public string Name => "On"; }

[State(Terminal = true)]
public partial record BrokenState(string Reason) : ILightState { public string Name => "Broken"; }

// Events
[Event]
[RichTransition(typeof(IdleState), typeof(OnState))]
public partial record TurnOnEvent(int Brightness) : ILightEvent { public string Name => "TurnOn"; }

[Event]
[RichTransition(typeof(OnState), typeof(IdleState))]
public partial record TurnOffEvent() : ILightEvent { public string Name => "TurnOff"; }

[Event]
[RichTransition(typeof(IdleState), typeof(BrokenState))]
[RichTransition(typeof(OnState), typeof(BrokenState))]
public partial record BreakEvent(string Reason) : ILightEvent { public string Name => "Break"; }
