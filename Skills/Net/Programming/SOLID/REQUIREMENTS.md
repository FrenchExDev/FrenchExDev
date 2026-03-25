# SOLID — Requirements

Non-negotiable rules Claude Code must enforce when working with this codebase.

## Interface Segregation

- **Interfaces at infrastructure seams must have exactly 1 method.** If you need 2 methods, split into 2 interfaces. The only exception is `IVosBackend` which groups related backend operations behind a `SupportedActions` capability check.
- **Never add a second method to an existing 1-method interface.** Create a new interface instead.

## Dependency Inversion

- **Never require a DI container.** Use optional constructor parameters with sensible defaults:
  ```csharp
  public MyOrchestrator(ISomething? something = null)
  {
      _something = something ?? new DefaultSomething();
  }
  ```
- **Never use service locator pattern.** Dependencies must be explicit in constructors.
- **Never use `new ConcreteService()` inside business logic.** Only in constructor defaults or factory methods.

## Testing

- **Every interface must have a hand-written Fake** in `test/.../Fakes/`. No mocking frameworks (Moq, NSubstitute, FakeItEasy) are permitted.
- **Fakes must be `internal sealed class`** and implement only the interface contract.
- **Fakes must accept their return values via constructor** — they are configurable stubs, not smart mocks.

## Open/Closed

- **Extension points must not break existing consumers.** New properties on models must have defaults. Existing emitters must work unchanged when a new extension point is added.
- **Never modify `BuilderEmitter.Emit()` for domain-specific behavior.** Use `BuilderEmitModel.Preamble` and `BuilderPropertyModel` extension points instead.
- **New strategies must be additive.** Adding a new `Instantiation` strategy must not change the behavior of existing strategies.

## Single Responsibility

- **Static analyzers must be stateless.** Pure static classes, no instance fields. Input in, output out. No side effects.
- **One analyzer per metric family.** Never combine unrelated metrics in a single analyzer class.

## Liskov

- **Backend interfaces must advertise capabilities** via a property like `SupportedActions`. Callers must check before invoking.
- **Unsupported operations must return a typed error** (e.g., `VosError.UnsupportedAction`), never throw, never silently succeed.
