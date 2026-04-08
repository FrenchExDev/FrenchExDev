# OPTIONS-PATTERN — Philosophy

`Option<T>` makes value absence explicit and type-safe. It is **not** an error type — `Result<T>` is for failures. Option is for "the value may or may not be there, and both states are normal."

## `null` Means Too Many Things

`null` in C# means "not found", "not initialized", "not applicable", "error", and "I forgot to set this". The type system can't tell them apart. A `string?` return type tells the caller nothing about whether absence is expected or a bug.

`Option<T>` means exactly one thing: the value may or may not be present, and both states are normal domain outcomes.

- A method returning `Option<User>` says "this user might not exist, and that's fine."
- A method returning `User` says "this user must exist."
- A method returning `Result<User>` says "looking up this user is an operation that may fail."

The three signatures communicate intent that `null` cannot.

## NRTs Are Not Enough

Nullable reference types help at the warning level but do not enforce at the type level. You can still pass `null` where `string` is expected and get a runtime crash. `Option<T>` with `T : notnull` enforces absence handling at compile time through `Match`, `Map`, and `Bind` — there is no way to access the inner value without acknowledging that it might not be there.

## Sealed Record For Value Semantics

`Option<T>` is a `sealed record` because:

1. **Value equality** — `Option.Some(42) == Option.Some(42)` is `true`. Options work in collections, dictionaries, and assertions without overriding `Equals`/`GetHashCode`.
2. **Immutability** — once created, an option cannot change.
3. **Pattern matching** — `sealed record` works with C# pattern matching, `with` expressions, and destructuring.
4. **Free `ToString()`** — `Some(42)` / `None` for free.

A `struct` was considered but rejected: struct options would box on every interface call and have surprising copy semantics around the `_isSome` field.

## Private Constructors, Public Factories

`Option<T>` has no public constructors. Construction happens through `Some(value)` or `None()`. This enforces two invariants:

1. `Some` always contains a non-null value — the factory throws on `null`.
2. There is no third state — every option is either Some or None.

Without private constructors, someone could `new Option<string>()` and get an uninitialized option claiming to be None but never intentionally created as one.

## LINQ Query Syntax Because It Reads Like English

```csharp
var result = from user in FindUser(id)
             from email in user.PrimaryEmail
             where email.IsVerified
             select email.Address;
```

This reads almost like natural language. The LINQ integration requires three methods (`Select`, `SelectMany`, `Where`) that map directly to `Map`, `Bind`, and `Filter`. No magic — just method resolution.

Method syntax (`FindUser(id).Bind(u => u.PrimaryEmail).Filter(e => e.IsVerified).Map(e => e.Address)`) is equally valid. Both compile to the same code. Having both costs three one-liner methods.

## Result Integration Because Absence Sometimes Becomes An Error

`Option<T>` and `Result<T>` are complementary:

- **Option**: "is it there?" (presence/absence)
- **Result**: "did it work?" (success/failure)

Sometimes you cross the boundary. A dictionary lookup returns `Option<Config>`. The calling code needs the config to exist — absence is now an error. `option.ToResult("Config not found")` converts absence to failure.

Going the other way, `result.ToOption()` discards the error and keeps only the success value. Useful when collecting results and only the successes matter (`results.Select(r => r.ToOption()).Values()`).

The integration is bidirectional and lazy — `ToResult(errorFactory)` only invokes the factory when the option is actually None.

## `Sequence` And `Traverse` Because All-Or-Nothing Is Common

A list of `Option<T>` often needs to become an `Option<List<T>>`: either all values are present, or the whole batch fails.

```
[Some(1), Some(2), Some(3)].Sequence()  → Some([1, 2, 3])
[Some(1), None, Some(3)].Sequence()     → None
```

`Traverse` combines mapping and sequencing: apply a function returning `Option<TOut>` to each element, and if any returns `None`, the whole thing is `None`. These are standard `Traversable` operations from functional programming and eliminate the "loop, check, bail, collect" pattern.

## Property-Based Tests Because Examples Lie

Functor laws (`Map(id) == id`, `Map(f).Map(g) == Map(g ∘ f)`) and monad laws (left/right identity, associativity) hold for *all* values, not just the ones in your examples. Verify them with a property-based testing library (CsCheck, FsCheck) so the algebraic guarantees aren't just "it works on my test cases."

## Anchor Package

[`Net/FrenchExDev/Options/`](../../../Net/FrenchExDev/Options/) — `Option<T>` sealed record, full functor/monad operations, LINQ, async pipelines, collection ops (`Sequence`/`Traverse`), bidirectional `Result` integration.
