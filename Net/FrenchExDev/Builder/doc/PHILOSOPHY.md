# Builder — Philosophy

## The core problem

Object construction in real applications is not a one-liner. It involves:

1. **Async I/O** — fetch related entities, call external services, generate files
2. **Validation** — enforce business invariants before spending resources on construction
3. **Graphs** — domain models reference each other; circular ownership is common
4. **Concurrency** — multiple request handlers may need the same object simultaneously

Standard C# constructors handle none of this. They are synchronous, have no validation layer, no cycle-safety, and no single-flight semantics.

The usual workaround is hand-written factory methods or builder classes. These work, but every team writes them differently, they're easy to get wrong (race conditions, missed validations, infinite loops in circular graphs), and they produce a lot of boilerplate.

---

## The design choices

### Validation is separated from construction

```
ValidateAsync() → if OK → Instantiate()
```

Validation runs first and **always runs completely** — it accumulates all errors, not just the first one. Only if validation passes does construction begin. This means:

- `CreateAsync` / `InstantiateAsync` can trust its inputs unconditionally
- Callers get a complete error report in one round-trip
- No defensive null-checks inside construction logic

This is the opposite of the common pattern where constructors throw `ArgumentNullException` on the first bad argument. That pattern forces callers to fix errors one at a time.

### Failure is a value — exceptions gain more value, not less

`Exception` is not abandoned. It is promoted.

Traditionally, `Exception` has a single role: to be thrown and interrupt control flow. The Result pattern gives exceptions a **second role**: they can flow through the system as ordinary values, accumulated, inspected, transformed, and returned — without ever being thrown.

```
Traditional role:   throw new CustomerNotFoundException(...)
                    ↑ unwinds the stack, caller must catch

New role:           Result<Order, CustomerNotFoundException>.Failure(new CustomerNotFoundException(...))
                    ↑ flows as a value, caller reads .Error, no stack unwind
```

Both roles are valid. The distinction is:

- **Throw** when something is genuinely unexpected and the caller cannot reasonably handle it inline (out-of-memory, programming error, unrecoverable state).
- **Return as a value** when the outcome is a known, named business condition that the caller must handle (not found, conflict, quota exceeded, invalid input).

```csharp
// Before Result: caller must intercept control flow to understand domain outcomes
Order order;
try { order = await factory.CreateOrderAsync(dto); }
catch (CustomerNotFoundException) { return NotFound(); }
catch (OrderLimitExceededException) { return UnprocessableEntity("Limit exceeded"); }

// With Result: outcome is in the return type — no exception as control flow
Result<Order, OrderCreationException> result = await builder.BuildAsync();
return result.Match(
    onSuccess: order => Created(order),
    onFailure: ex    => ex switch
    {
        { Message: var m } when m.Contains("not found") => NotFound(),
        _                                               => UnprocessableEntity(ex.Message)
    }
);
```

The `Exception` object still carries all its usual information — message, stack trace, inner exceptions. It just also happens to be a value that can be passed around, logged, mapped, or recovered from, without altering control flow.

`[Builder(Exception = typeof(E))]` makes the **typed failure path explicit in the API signature** — the caller knows exactly what kind of failure is possible without reading source or documentation:

```csharp
// The return type tells you everything:
Task<Result<Order, OrderCreationException>> BuildAsync(CancellationToken ct)
//                  ↑ zero ambiguity: this is the only domain failure you need to handle
```

`ValidationResult` follows the same principle: each validation error is an `Exception` instance (carrying message, type, context) that flows as a value rather than being thrown.

### Circular graphs are a first-class concern

Most builder frameworks break on circular object references. This one does not. The solution is `VisitedObjects` — a thread-safe dictionary keyed by **reference equality** that tracks which builders are currently executing.

The protocol:
1. Call `reference.Resolve(instance)` **immediately after creating the instance**, before building any nested objects
2. Pass `visitedObjects` into every nested `BuildAsync` call
3. When a builder is called recursively (back into itself), `IsVisited` returns `true` and the already-resolving reference is returned — no infinite loop

This requires the developer to write manual builders for graph nodes. The generator is not involved here because graphs require explicit control over when `reference.Resolve` is called.

### Single-flight is automatic

A builder instance is a unit of work. Once it starts building, any concurrent caller that arrives before the result is ready will wait on the same semaphore, then receive the same cached `Reference<T>`. The object is never built twice.

This is not just an optimization. It is a correctness guarantee for graphs: if two builders both need the same parent node, they must receive the same instance, not two separate copies.

### The source generator removes boilerplate, not control

The generator writes:
- Input property declarations
- Virtual validation hooks (one per property, one per collection item)
- A `ValidateAsync` override that calls all hooks
- A default `BuildException` that produces a readable error message
- A sealed `Instantiate` bridge that calls `CreateAsync`

Everything the generator produces is `virtual` or `partial` — fully overridable. The generator accelerates the 90% case. The 10% that needs custom logic (cross-property validation, async checks, graph construction) is handled in plain C# code.

---

## What this is not

**Not an ORM or DI container.** The builder produces one specific object per invocation. It does not manage lifetimes, scopes, or registrations.

**Not a mapper.** Properties on the builder are explicitly set by the caller, not automatically mapped from a DTO.

**Fluent API is generated.** Every input property gets a matching `With{Prop}(value)` method that sets the property and returns `this`. Both styles — plain property setter and method chaining — work out of the box.

**Not a test fixture factory.** Although builders are convenient in tests, they are designed for production use — thread safety, validation, and async construction are all production concerns.

---

## Relation to the Result package

The Builder package depends on `FrenchExDev.Net.Result` for two things:

1. `Result<ValidationResult>` — the return type of `ValidateAsync`
2. `Result<T, TException>` — the return type of `BuildAsync` in the exception-aware variant

This keeps errors as values through the entire call stack, from validation through construction through the caller.

---

## Trade-offs accepted

| Trade-off | Decision |
|---|---|
| Developers must write `CreateAsync` / `InstantiateAsync` | No way around it — construction is application-specific |
| Manual builders require more code | Full control over `Resolve` timing is necessary for graphs |
| `Reference<T>` adds one layer of indirection | Required for single-flight caching before the value exists |
| The generator hides `Reference<T>` entirely | Simpler developer API — most builders never need to see it |
| `TypedBuildException` instead of covariant override | `netstandard2.0` runtime doesn't support covariant return types |
