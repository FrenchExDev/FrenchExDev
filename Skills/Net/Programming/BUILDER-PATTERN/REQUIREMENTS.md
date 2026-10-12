# BUILDER-PATTERN — Requirements

Use this checklist before adopting the async builder pattern in a new package, and as the acceptance criteria for any builder you write.

## When to Reach for a Builder

A builder is justified if **at least one** of the following is true:

- [ ] Construction touches I/O (database, network, file system, hash, hash-of-hash)
- [ ] Construction can fail with multiple validation errors that the caller wants together
- [ ] The target object participates in a cycle (any back-reference)
- [ ] Two or more concurrent callers may need the same instance
- [ ] The object has more than ~5 input properties and a constructor would be unreadable
- [ ] The construction logic is generated from a schema or attribute (i.e. by a source generator)

If none of these are true, prefer a constructor or a factory method. Builders are not free.

## Mode Selection

Pick exactly one:

| If… | Use |
|---|---|
| Failures are exceptional, validation is the main concern | `[Builder]` (Mode 1) |
| Failures are domain-meaningful and need a typed signature | `[Builder(Exception = typeof(E))]` (Mode 2) |
| The object graph has back-references | Manual `AbstractBuilder<T>` (no attribute) |
| You are emitting builders from another source generator | Call `BuilderEmitter.Emit(model)` from `.Lib` |

Do not mix modes on the same builder.

## Required Runtime Contracts

Every builder, no matter how it is produced, must obey:

- [ ] `BuildAsync` is the only public entry point. Do not add a synchronous `Build()`.
- [ ] `BuildAsync` is idempotent. The Nth call returns the same `Reference<T>` as the first.
- [ ] `BuildAsync` is single-flight. Concurrent callers receive the same instance.
- [ ] Validation runs before construction. Construction must trust its inputs.
- [ ] Validation accumulates every error. It does not stop at the first.
- [ ] If validation fails, construction is not invoked at all.
- [ ] Errors are values. `Validate*` hooks **yield** exceptions; they never **throw**.
- [ ] `CancellationToken` is honoured at every `await` point.

## Required for Manual Graph Builders

If you write a builder by hand (no `[Builder]` attribute) for a graph node:

- [ ] `Instantiate` calls `reference.Resolve(instance)` **before** building any nested object.
- [ ] Every nested `BuildAsync` call receives the inherited `visitedObjects`.
- [ ] Every nested `BuildAsync` call receives the inherited `cancellationToken`.
- [ ] The builder constructor accepts the inputs it needs; no public mutator after construction.
- [ ] Cycle test exists: a unit test asserts that two builders sharing a back-reference build to a graph in which both directions resolve to the same instance.

Failure to follow rule 1 produces infinite recursion. Failure to follow rule 2 breaks cycle detection. These are not optional.

## Required for Mode 2 Builders

- [ ] The exception type is a real domain exception, not `Exception` or `InvalidOperationException`.
- [ ] `InstantiateAsync` is implemented (it is abstract on `AbstractBuilder<T, TException>`).
- [ ] `InstantiateAsync` returns `Result<T, TException>.Failure(...)` for known failures, never throws.
- [ ] If error translation is needed, override `TypedBuildException` (not the sealed `BuildException`).

## Required for Source-Generator-Authored Builders

If you call `BuilderEmitter` from your own source generator:

- [ ] The generator project references `FrenchExDev.Net.Builder.SourceGenerator.Lib` only — never the Roslyn-aware `SourceGenerator` package.
- [ ] The `BuilderEmitModel` is built from your own model, not by reading another generator's output.
- [ ] Customisation goes through `Preamble`, `WithMethodAttributes`, `WithMethodBodyPrefix`, `InstantiationExpression`. Do not fork the emitter.
- [ ] Generated builders carry `[GeneratedCode("YourGenerator", "version")]` so coverage tools can ignore them.

## Validation Quality Bar

Every `Validate*` override must:

- [ ] Yield exceptions, never throw
- [ ] Be deterministic (no I/O, no clock, no RNG)
- [ ] Be cheap (validation runs every `BuildAsync` until success)
- [ ] Use `ArgumentNullException` / `ArgumentOutOfRangeException` / `FormatException` for kind matching the failure
- [ ] Include the offending value in the message where safe

I/O-bound checks belong in `InstantiateAsync` (Mode 2), not in validation hooks.

## Testing Quality Bar

Every builder needs at least:

- [ ] One happy-path test that asserts `BuildAsync` succeeds and properties round-trip
- [ ] One validation-failure test per `Validate*` override
- [ ] One single-flight test if the builder might be hit concurrently — N parallel `BuildAsync` calls produce the same instance and one underlying construction
- [ ] One cycle test if the builder participates in a graph
- [ ] One cancellation test asserting `OperationCanceledException` (or a clean failure result) on a cancelled token

Use hand-written fakes — no mocking framework.

## Things You Must Never Do

- Throw inside a `Validate*` hook (use `yield return` of an exception value)
- Override the sealed `Instantiate` bridge (override `CreateInstance` or `InstantiateAsync` instead)
- Cache build results outside the builder
- Build a graph without `reference.Resolve(instance)` early
- Reuse a `VisitedObjects` across unrelated build root operations
- Pass a builder around as `IBuilder<T>` — there is no such interface, and there shouldn't be (each builder's `With*` surface is its public API)
- Add a synchronous `.Build()` overload "for convenience"
- Make `CreateInstance` do I/O (it's synchronous; use Mode 2 + `InstantiateAsync` for I/O construction)
