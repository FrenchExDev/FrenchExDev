# Options

Functional `Option<T>` type for C# -- represents a value that may or may not be present. Unlike `null`, absence is explicit and type-safe. Unlike `Result<T>`, absence is not an error. Includes functor/monad operations (`Map`, `Bind`, `Filter`), LINQ query syntax, async pipelines, collection extensions (`Values`, `FirstOrNone`, `Sequence`, `Traverse`), bidirectional `Result<T>` integration, and property-based tests via CsCheck.

## Quick Start

```csharp
// Create options
var some = Option.Some(42);
var none = Option.None<int>();
var fromRef = Option.From<string>(maybeNull);     // Some or None based on null
var fromVal = Option.FromNullable<int>(nullableInt); // Same for Nullable<T>
var safe = Option.FromTry(() => int.Parse(input));   // Some or None on exception

// Pattern match
string label = some.Match(
    onSome: v => $"Got {v}",
    onNone: () => "Nothing");

// Pipeline
var result = Option.From(user)
    .Filter(u => u.IsActive)
    .Map(u => u.Email)
    .Bind(email => LookupMailbox(email))
    .OrDefault("no-reply@example.com");

// LINQ query syntax
var combined = from x in optionA
               from y in optionB
               where x + y > 10
               select x + y;

// Async pipeline
var email = await Option.From(userId)
    .BindAsync(id => FindUserAsync(id))
    .MapAsync(u => u.Email)
    .OrDefaultAsync("unknown");

// Result integration
Result<User> result = Option.From(user).ToResult("User not found");
Option<User> back = result.ToOption();
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `Options` | netstandard2.0; net10.0 | `Option<T>`, extensions (Map, Bind, Filter, Tap, Zip, LINQ, async, collections, Result integration) |
| `Options.Testing` | netstandard2.0; net10.0 | `OptionAssertions` (ShouldBeSome, ShouldBeNone, ShouldBeSomeAnd) |
| `Options.Tests` | net10.0 | 126 xUnit + CsCheck property-based tests |

## Extension Categories

| File | Extensions |
|------|-----------|
| `Extensions.cs` | Map, Bind, Then, Filter, Tap, TapNone, OrDefault, OrElse, Or, ToNullable, ToNullableStruct, Zip, Contains |
| `AsyncExtensions.cs` | MatchAsync, MapAsync, BindAsync, TapAsync, WhereAsync, OrDefaultAsync, OrElseAsync (on both `Option<T>` and `Task<Option<T>>`) |
| `CollectionExtensions.cs` | Values, FirstOrNone, SingleOrNone, GetValueOrNone, Sequence, Traverse |
| `LinqExtensions.cs` | Select (Map), SelectMany (Bind), Where (Filter) -- enables LINQ query syntax |
| `ResultIntegration.cs` | ToResult, ToOption, OrResult, BindResult -- bidirectional Option/Result conversion |

## Key Design Decisions

- **Sealed record** -- value equality, immutable, pattern-matching friendly, `ToString()` built-in
- **`notnull` constraint** -- `Option<T>` where `T : notnull` prevents `Option<string?>` nonsense
- **Implicit conversion** -- `Option<string> opt = "hello"` works via implicit operator
- **Exhaustive matching** -- `Match` requires both branches, `Switch` for side effects
- **Result integration** -- `ToResult("error")` bridges absence to failure, `ToOption()` discards errors

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- type design, extension organization, Result integration, algebraic properties
- [HOW-TO.md](doc/HOW-TO.md) -- creation, matching, pipelines, async, collections, LINQ, Result bridges, testing
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why Option over null, why sealed record, why LINQ support, why Result integration

## Building

```bash
dotnet build Options/FrenchExDev.Net.Options.slnx
dotnet test Options/FrenchExDev.Net.Options.slnx
```
