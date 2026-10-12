# Reactive — Claude Context

Domain-oriented event stream library — `IEventStream<out T>` and `EventStream<T>` (Subject-backed) wrap `System.Reactive` behind a small surface using domain language (`Publish`, `Filter`, `Map`, `Throttle`) instead of Rx jargon (`OnNext`, `Where`, `Select`).

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [REACTIVE](../../../Skills/Net/Programming/REACTIVE/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Reactive.slnx`

## Notes for Claude
- README and `doc/*.md` files are currently empty stubs — derive intent from `src/FrenchExDev.Net.Reactive/*.cs`.
- `IEventStream<out T>` is the consumer interface — `Subscribe` and `AsObservable()` only. Producer methods (`Publish`, `Complete`, `Error`) live ONLY on the concrete `EventStream<T>` class. Never expose them via the interface.
- `EventStream<T>` is backed by `Subject<T>` — it's a HOT observable. Late subscribers miss earlier events. For replay scenarios, use `ReplaySubject<T>` via the escape hatch.
- Operators (`Filter`, `Map`, `Merge`, `Throttle`, `Buffer`, `DistinctUntilChanged`, `Take`, `Skip`, `OfType`) all delegate to Rx via `stream.AsObservable().{Op}(...)` and wrap the result in the `internal` `ObservableEventStream<T>` adapter.
- Operator names use domain language. Never expose `Where`/`Select`/`OnNext` to consumers.
- `EventStream<T>` is `IDisposable` — the producer must dispose it on shutdown. Subscribers also receive an `IDisposable` from `Subscribe(...)` to remove themselves.
- `AsObservable()` is the deliberate escape hatch for code that needs the full Rx surface.
