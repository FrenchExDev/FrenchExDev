# REACTIVE — Requirements

## Core Interface

- [ ] `IEventStream<out T>` — covariant, read-only consumer interface.
- [ ] Provides `Subscribe(Action<T> onNext, Action<Exception>? onError = null, Action? onCompleted = null)` returning `IDisposable`.
- [ ] Provides `AsObservable()` returning `IObservable<T>` as the Rx escape hatch.
- [ ] Does NOT expose any way to push events from the consumer side.

## Producer Class

- [ ] `EventStream<T>` is a `sealed class` implementing `IEventStream<T>` and `IDisposable`.
- [ ] Backed by `Subject<T>` from `System.Reactive.Subjects`.
- [ ] Exposes `Publish(T value)`, `Complete()`, `Error(Exception)` — these are NOT on the interface.
- [ ] `Dispose()` disposes the underlying subject.

## Operator Pattern

- [ ] Every operator is an extension method on `IEventStream<T>`.
- [ ] Every operator null-checks its arguments.
- [ ] Every operator delegates to Rx via `stream.AsObservable().{RxOp}(...)`.
- [ ] Every operator wraps the result in an internal `ObservableEventStream<T>` adapter.
- [ ] Operators return new streams — they do NOT mutate the source.

## Required Operators

- [ ] `Filter(predicate)` (Rx `Where`)
- [ ] `Map(mapper)` (Rx `Select`)
- [ ] `Merge(other)` (Rx `Merge`)
- [ ] `Buffer(int count)` and `Buffer(TimeSpan window)`
- [ ] `Throttle(TimeSpan window)`
- [ ] `DistinctUntilChanged()`
- [ ] `Take(int count)`
- [ ] `Skip(int count)`
- [ ] `OfType<TTarget>()` on `IEventStream<object>`

## Domain Naming

- [ ] Operator names use **domain language** (`Filter`, `Map`), NOT Rx jargon (`Where`, `Select`).
- [ ] The interface uses domain language (`Subscribe`, `Publish`), NOT Rx jargon (`OnNext`, `IObserver`).
- [ ] Rx names appear ONLY when the consumer drops into `AsObservable()`.

## Internal Adapter

- [ ] `ObservableEventStream<T>` is `internal sealed`.
- [ ] Wraps an `IObservable<T>` to present it as `IEventStream<T>`.
- [ ] Application code MUST NOT reference it directly.

## Disposal

- [ ] `EventStream<T>` is `IDisposable`.
- [ ] Disposing the stream signals completion to subscribers and releases the subject.
- [ ] Each `Subscribe(...)` call returns an `IDisposable` that removes only that subscriber.

## Hot vs Cold

- [ ] `EventStream<T>` is **hot** — subscribers only receive events published after they subscribe.
- [ ] No built-in replay or caching. Replay scenarios use `ReplaySubject<T>` via the escape hatch.

## Test Double

- [ ] `Reactive.Testing` package with a `TestEventStream<T>` (or equivalent) for unit tests.
- [ ] Time-sensitive operators (`Throttle`, `Buffer(timeSpan)`) tested via Rx `TestScheduler` through the escape hatch.

## What MUST NOT Be Done

- [ ] `IEventStream<T>` MUST NOT expose `Publish`, `Complete`, or `Error`.
- [ ] Operators MUST NOT mutate the source stream.
- [ ] Application code MUST NOT use `Where`/`Select`/`Throttle` from `System.Reactive.Linq` directly — use the domain operators or drop into `AsObservable()` explicitly.
- [ ] Producer-side `EventStream<T>` MUST NOT be exposed via DI as the consumer interface — register both separately so consumers receive `IEventStream<T>`.

## Anchor Package

[`Net/FrenchExDev/Reactive/`](../../../Net/FrenchExDev/Reactive/) — implements every requirement.
