# FINITE-STATE-MACHINE — Requirements

Acceptance criteria for any state machine built on this library.

## Tier Selection

Pick exactly one tier per machine:

- [ ] Dynamic — only if states/events come from runtime input (JSON, DB, end-user editor)
- [ ] Typed — for compile-time-known machines without state payloads (the default)
- [ ] Rich — only when states or events carry data the engine needs to forward

Don't mix tiers in one machine. Each tier has a different definition shape.

## Definition Quality Bar

Every state machine definition must:

- [ ] Declare exactly one initial state
- [ ] Declare at least one final state, or be explicitly cyclic with a clear documented invariant
- [ ] Have no orphan states (every state is reachable from the initial state)
- [ ] Have no duplicate `(From, Event)` transitions — the second registration must error at build time
- [ ] Build successfully (`def.IsSuccess == true`) — never `Build()` and ignore the result

## Async Discipline

- [ ] Every fire site uses `await machine.FireAsync(...)`. No `.Result`, no `.Wait()`.
- [ ] Every guard, action, and listener method returns `Task` and propagates `CancellationToken`.
- [ ] Long-running guards or actions check cancellation regularly.

## Result Handling

`FireAsync` returns `Result<Transition<TState>>`. Every fire site must:

- [ ] Inspect the result with `Match`, `IsSuccess`, or pattern matching
- [ ] Treat denial as a normal outcome (not a bug)
- [ ] Not catch exceptions to detect denial — denials are values, not throws

If you see a `try/catch` around `FireAsync`, the code is wrong: actions and guards already wrap their exceptions in `Result.Failure(ex)`.

## Source Generator Compliance (Typed Tier)

If you use `[StateMachine]` + `[Transition]`:

- [ ] The class is `partial`
- [ ] `[StateMachine]` declares the state and event enum types
- [ ] `[StateMachine]` declares `InitialState = nameof(...)`
- [ ] `[Transition]` is repeated for every transition; no manual builder calls in the same class
- [ ] No hand-written `FireXxxAsync` overloads — they would shadow the generated ones

## Source Generator Compliance (Rich Tier)

- [ ] The class is `partial`
- [ ] States implement a marker interface that derives from `IState`
- [ ] Events implement a marker interface that derives from `IEvent`
- [ ] States and events are records (or otherwise immutable)
- [ ] Transition lambdas are pure functions of `(event, state)`

## Listener Quality Bar

- [ ] Listeners implement `IStateMachineListener` directly — no inheritance from a base class that forces all hooks
- [ ] Default to `Task.CompletedTask` for hooks the listener doesn't care about
- [ ] Never call `FireAsync` from inside a listener hook (use deferred events instead)
- [ ] Listener exceptions are tolerated by the engine but should still be logged at the listener side

## Hierarchy Compliance

- [ ] Use `IsChildOf(parent)` to declare hierarchy explicitly
- [ ] Document the parent's role: which transitions / actions are inherited, which are overridden
- [ ] Avoid hierarchies deeper than three levels — readability suffers

## Composite Machine Compliance

- [ ] Each region is a complete state machine in its own right
- [ ] Regions are orthogonal (their states do not refer to each other)
- [ ] Cross-region coordination uses listeners + deferred events, not shared mutable state
- [ ] At least one region must accept an event for the composite to report success

## Testing Quality Bar

Every machine needs at least:

- [ ] One happy-path `PathReachesAsync` test that walks from initial to final
- [ ] One denial test per known-illegal `(state, event)` pair
- [ ] One guard test per guarded transition (both pass and fail)
- [ ] One model-based test using `StateMachinePathGenerator.AllPaths(def, maxDepth)` for machines with > 5 states
- [ ] One visualization test that asserts the Mermaid output matches a fixture (catches accidental model changes)

For source-generated machines, also:

- [ ] One emitter test in the `.Lib` test project asserting the emitted source

## Concurrency Mode Selection

- [ ] Default is `ConcurrencyMode.Semaphore`. Use this unless you have measured a problem.
- [ ] `ConcurrencyMode.None` is justified only when callers serialize externally **and** the lock is on the hot path.
- [ ] Switching to `None` requires a code comment explaining the serialization invariant.

## Visualization Compliance

- [ ] Every long-lived state machine has a `MermaidExporter.Export(...)` call wired into the build
- [ ] The diagram is checked into source control
- [ ] The diagram is regenerated on every build (or a CI check fails on drift)

A state machine that can't be visualized is a state machine the team will eventually refuse to maintain.

## Things You Must Never Do

- Block on `FireAsync().Result`
- Throw to signal a denied event (use `Result.Failure`)
- Catch exceptions around `FireAsync` to detect denial
- Hand-write the cross-product of regions as flat states
- Re-declare the same shared transition on every leaf state — use hierarchy
- Call `FireAsync` from inside a listener (use deferred events)
- Mix Typed and Rich tier syntax in the same machine
- Use Dynamic for compile-time-known machines (you lose every typo check)
- Hand-write `FireXxxAsync` methods that shadow generated ones
- Skip the result check on `Build()` — failures are real
