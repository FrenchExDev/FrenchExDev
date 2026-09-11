# REACTIVE — Claude Context

Domain-oriented event stream wrapping `System.Reactive` behind `IEventStream<out T>` (covariant, read-only). Domain-named operators (Publish, Subscribe, Filter, Map) instead of Rx jargon. `AsObservable()` escape hatch for advanced scenarios.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
None explicitly.

## Related packages
- [`FrenchExDev.Net.Reactive`](../../../../Net/FrenchExDev/Reactive/)

## Notes for Claude
- Hot observables (backed by `Subject<T>`) — late subscribers miss earlier events
- For replay, use `ReplaySubject<T>` via `AsObservable()` escape hatch
- Never expose `EventStream<T>` (producer) to consumers — expose `IEventStream<T>` only
- Subscription disposal removes only that subscriber, not the stream
- Operators return new streams; source is never mutated
