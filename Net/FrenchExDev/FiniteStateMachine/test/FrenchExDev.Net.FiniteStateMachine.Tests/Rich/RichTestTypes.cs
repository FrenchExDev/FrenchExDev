using FrenchExDev.Net.FiniteStateMachine.Rich;

namespace FrenchExDev.Net.FiniteStateMachine.Tests.Rich;

// State interface
public interface IOrderState : IState { }

// Concrete states
public record CreatedState() : IOrderState { public string Name => "Created"; }
public record SubmittedState(DateTime SubmittedAt) : IOrderState { public string Name => "Submitted"; }
public record ApprovedState(string ApprovedBy) : IOrderState { public string Name => "Approved"; }
public record ShippedState(string TrackingNumber) : IOrderState { public string Name => "Shipped"; }
public record DeliveredState(DateTime DeliveredAt) : IOrderState { public string Name => "Delivered"; }
public record CancelledState(string Reason) : IOrderState { public string Name => "Cancelled"; }

// Event interface
public interface IOrderEvent : IEvent { }

// Concrete events
public record SubmitEvent(List<string> Items) : IOrderEvent { public string Name => "Submit"; }
public record ApproveEvent(string ApprovedBy) : IOrderEvent { public string Name => "Approve"; }
public record ShipEvent(string TrackingNumber) : IOrderEvent { public string Name => "Ship"; }
public record DeliverEvent() : IOrderEvent { public string Name => "Deliver"; }
public record CancelEvent(string Reason) : IOrderEvent { public string Name => "Cancel"; }
