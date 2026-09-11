using FrenchExDev.Net.FiniteStateMachine.Rich;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Engine.RichSimple;

public interface ISimpleState : IState { }
public interface ISimpleEvent : IEvent { }
public record StateA() : ISimpleState { public string Name => "A"; }
public record StateB() : ISimpleState { public string Name => "B"; }
public record GoEvent() : ISimpleEvent { public string Name => "Go"; }
