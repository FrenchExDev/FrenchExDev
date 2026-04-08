# Mediator — Claude Context

Lightweight CQRS mediator contracts: `IMediator`, `IRequest`/`ICommand`/`IQuery`, `IRequestHandler`, `IBehavior` (pipeline middleware), `INotification`/`INotificationHandler`, and `PublishStrategy`. **Contracts only — no runtime implementation.**

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [MEDIATOR-PATTERN](../../../Skills/Net/Programming/MEDIATOR-PATTERN/PHILOSOPHY.md)
- [CQRS](../../../Skills/Net/Programming/CQRS/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Mediator.slnx`

## Notes for Claude
- 7 interfaces, 1 enum, 0 runtime classes. Never add a `Mediator` concrete class — the runtime is consumer-specific.
- `ICommand<TResult>` and `IQuery<TResult>` both extend `IRequest<TResult>`. They are dispatched identically — they exist as semantic markers so behaviors can target them via generic constraints.
- `IBehavior<TRequest, TResult>` takes a `Func<Task<TResult>> next` delegate — middleware pattern, NOT decorator. Behaviors may short-circuit by NOT calling `next()`.
- `PublishStrategy` is a parameter on `PublishAsync`, NOT a global setting. Per-call strategy is intentional.
- Depends on `FrenchExDev.Net.Result` so handlers can return `Task<Result<T>>`.
- `FakeMediator` records sent requests/notifications and uses `Setup<TRequest, TResult>` for canned responses. It does NOT verify expectations — assertions are explicit via `WasSent<T>()` etc.
- Targets `netstandard2.0 + net10.0`. 11 tests cover the FakeMediator.
