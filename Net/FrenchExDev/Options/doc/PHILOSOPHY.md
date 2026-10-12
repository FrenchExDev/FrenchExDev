# Options -- Philosophy

## Option over null

`null` means too many things in C#. It means "not found", "not initialized", "not applicable", "error", and "I forgot to set this." The type system can't distinguish between them. A `string?` return type tells you nothing about whether absence is expected or a bug.

`Option<T>` means exactly one thing: the value may or may not be present, and both states are normal. A method returning `Option<User>` says "this user might not exist, and that's fine." A method returning `User` says "this user must exist." The type communicates intent that `null` cannot.

Nullable reference types (NRTs) help at the warning level but don't enforce at the type level. You can still pass `null` where `string` is expected and get a runtime crash. `Option<T>` with `T : notnull` enforces absence handling at compile time through `Match`, `Map`, and `Bind` -- there is no way to access the value without acknowledging that it might not be there.

---

## Sealed record for value semantics

`Option<T>` is a `sealed record` because:

1. **Value equality** -- `Option.Some(42) == Option.Some(42)` is `true`. This means options work correctly in collections, dictionaries, and assertions without overriding `Equals`/`GetHashCode`
2. **Immutability** -- once created, an option cannot change. No mutable state, no thread-safety concerns
3. **Pattern matching** -- `sealed record` works with C# pattern matching, `with` expressions, and destructuring
4. **`ToString()`** -- records generate a readable `ToString()` automatically (`Some(42)`, `None`)

A `struct` was considered but rejected: the default value of a struct would be `None` with `_isSome = false`, which is actually correct, but struct options would box on every interface call and have surprising copy semantics.

---

## Private constructors, public factories

`Option<T>` has no public constructors. You must use `Some(value)` or `None()`. This enforces two invariants:

1. `Some` always contains a non-null value -- the factory throws on `null`
2. There is no third state -- every option is either Some or None

Without this, someone could `new Option<string>()` and get an uninitialized option that claims to be None but wasn't intentionally created as one. Private constructors close that door.

---

## LINQ query syntax because it reads like English

```csharp
var result = from user in FindUser(id)
             from email in user.PrimaryEmail
             where email.IsVerified
             select email.Address;
```

This reads almost like natural language: "from the user, get their primary email, where it's verified, select the address." The LINQ integration requires three methods (`Select`, `SelectMany`, `Where`) that map directly to `Map`, `Bind`, and `Filter`. No magic -- just method resolution.

Not everyone likes query syntax. The method syntax (`FindUser(id).Bind(u => u.PrimaryEmail).Filter(e => e.IsVerified).Map(e => e.Address)`) is equally valid. Both compile to the same code. Having both costs three one-liner methods.

---

## Result integration because absence sometimes becomes an error

`Option<T>` and `Result<T>` are complementary:

- **Option**: "is it there?" (presence/absence)
- **Result**: "did it work?" (success/failure)

Sometimes you need to cross the boundary. A dictionary lookup returns `Option<Config>` (config may not exist). But the calling code needs it to exist -- absence is now an error. `option.ToResult("Config not found")` converts absence to failure with a specific error message.

Going the other direction, `result.ToOption()` discards the error and keeps only the success value. This is useful when you're collecting results and only care about the successes (e.g., `results.Select(r => r.ToOption()).Values()`).

The integration is bidirectional and lazy -- `ToResult(errorFactory)` only creates the error when the option is actually None.

---

## Sequence and Traverse because all-or-nothing is common

A list of `Option<T>` often needs to become an `Option<List<T>>`: either all values are present, or the whole batch fails. This is the `Sequence` operation:

```csharp
[Some(1), Some(2), Some(3)].Sequence()  → Some([1, 2, 3])
[Some(1), None, Some(3)].Sequence()     → None
```

`Traverse` combines mapping and sequencing: apply a function that returns `Option<TOut>` to each element, and if any returns `None`, the whole thing is `None`.

These operations come from Haskell's `Traversable` typeclass and are standard in functional programming. They eliminate the common pattern of "loop, check each result, bail on first failure, collect on success."

---

## Property-based tests because examples lie

Example-based tests prove that `Map` works for `Some(42)` and `None`. Property-based tests (via CsCheck) prove that `Map` satisfies the functor laws for *all* values:

- `option.Map(x => x)` is always equal to `option` (identity)
- `option.Map(f).Map(g)` is always equal to `option.Map(x => g(f(x)))` (composition)

The monad laws (left identity, right identity, associativity) are also verified. These laws guarantee that `Map`, `Bind`, and `Filter` compose predictably -- they're not just "it works for my test cases" but "it works for all inputs the type system allows."

13 property tests complement the 113 example tests. The properties catch category errors (off-by-one in None handling, broken equality) that specific examples might miss.

---

## Async extensions on Task<Option<T>> because await is contagious

One `async` call in a pipeline forces every subsequent step to `await`:

```csharp
// Without async extensions
var option = await GetUserAsync(id);
var mapped = option.Map(u => u.Name);
var result = mapped.OrDefault("unknown");
```

With async extensions on `Task<Option<T>>`:

```csharp
var result = await GetUserAsync(id)
    .MapAsync(u => u.Name)
    .OrDefaultAsync("unknown");
```

Every sync extension has an async counterpart that chains directly on `Task<Option<T>>`. This keeps pipelines flat and eliminates intermediate `var` declarations.
