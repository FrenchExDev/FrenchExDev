# CQRS — Requirements

Non-negotiable rules for CQRS implementation.

## Handler Interfaces

- **Commands and queries must use separate handler interfaces.** `ICommandHandler<TCommand, TResult>` for mutations, `IQueryHandler<TQuery, TResult>` for reads. Never mix both in one class.
- **`HandleAsync` is the only method.** Both handler interfaces have exactly one method. No additional methods. This is ISP.
- **Handler classes implement exactly one handler interface.** One handler per command/query. No God-handlers that handle multiple command types.

## Commands

- **Commands must declare their target aggregate.** `[Command("Name", AggregateRoot = "...")]` is required. Orphan commands (no aggregate) are a design smell.
- **Command handlers must use builders.** Flow: receive command -> construct via builder -> `EnsureInvariants()` -> persist -> raise events. Never construct aggregates directly via `new`.
- **Command handlers return `Result<T>`.** Commands may fail (validation, invariant violation, persistence error). The result type makes failure explicit.

## Queries

- **Query handlers are read-only.** They must not mutate state, persist data, or raise events.
- **Query results can be plain types.** Unlike commands, queries don't need `Result<T>` wrapping unless there's a genuine failure mode.

## Domain Events

- **Domain events must implement `IDomainEvent`.** This requires `DateTimeOffset OccurredAt`.
- **Events are raised after persistence, not before.** Only emit an event once the aggregate has been successfully saved. This prevents phantom events for failed operations.
- **Events must have `[DomainEvent("Name")]` attribute** with optional `SourceAggregate` property linking to the originating aggregate.

## No Mediator

- **Handlers are injected directly.** No MediatR, no message bus, no convention-based dispatch. The dependency graph must be explicit and traceable.
- **No runtime handler registration.** All handler dependencies are resolved at compile time via constructor injection.

## Backend Interfaces

- **Backend interfaces must separate mutations from queries.** Like `IVosBackend`: Up/Halt/Destroy are commands; Status/Ssh are queries. Make the split visible in the interface.
- **Unsupported operations return typed errors.** Not exceptions, not silent no-ops. See `VosError.UnsupportedAction`.
