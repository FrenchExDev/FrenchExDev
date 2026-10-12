# System.CommandLine v2 — Rules

## No `!` Operator

Never use the null-forgiving operator `!` on parsed values. Every nullable must be validated explicitly.

```csharp
// WRONG
var name = pr.GetValue(VosCliSymbols.Name)!;

// RIGHT
var name = pr.GetValue(VosCliSymbols.Name)
    ?? throw new RequiredOptionMissingException("name");
```

## No Silent Failures

Never swallow a null and return early. A dev CLI must surface every error.

```csharp
// WRONG — silent failure, user sees nothing
var orch = await CreateOrchestrator(pr.GetValue(configOption)!);
if (orch is null) return;

// RIGHT — explicit throw, user sees what went wrong
var config = await configService.LoadOrThrowAsync(input.ConfigPath);
var orch = new VosOrchestrator(new VagrantBackend(), config);
```

## Every Failure Is a Named Exception

No `throw new InvalidOperationException("some message")`. Every distinct failure mode has its own sealed class inheriting from the CLI's base exception.

```csharp
// WRONG
throw new InvalidOperationException($"Machine type '{name}' not found.");

// RIGHT
public sealed class MachineTypeNotFoundException(string name)
    : VosCliException($"Machine type '{name}' not found.");
```

The exception list is the specification of what can go wrong at runtime.

## Static Throw Class

All throw calls go through a static class with `[DoesNotReturn]` methods. No `throw new` scattered in business code.

```csharp
// Call site reads as intent, not plumbing
VosCliThrow.MachineTypeNotFound(name);
```

## Symbols Class Is Static

One `static` class per CLI tool. Owns all `Option<T>` and `Argument<T>` instances. No DI needed — symbols are stateless value descriptors.

```csharp
public static class VosCliSymbols
{
    public static Option<string> Config { get; } = new("--config") { ... };
}
```

Shared options ("inherited") are attached to multiple commands by referencing the same static property.

## Every Command Is Its Own Class

No inline command definitions in `Program.cs`. Each command inherits `Command` and receives DI dependencies in its constructor.

## Every Command with >2 Parsed Values Gets a Resolver

The resolver validates all inputs, throws on missing required values, and returns an immutable record. `ParseResult` never leaks past the resolver into domain code.

## Group Commands Have No Action

Group commands (`snapshot`, `type`, `machine`) are containers. They receive child commands via DI and call `Subcommands.Add(...)`. They never have a `SetAction`.

## Program.cs Is Wiring Only

`Program.cs` contains:
1. `ServiceCollection` registration
2. Root command assembly (resolve commands, add to `RootCommand`)
3. `root.Parse(args).InvokeAsync()`
4. Top-level `catch (VosCliException)` — prints to stderr, exits 1

No business logic, no option definitions, no helper methods.

## SetAction Always Takes CancellationToken

```csharp
// WRONG
command.SetAction((pr) => { ... });

// RIGHT
command.SetAction(async (pr, ct) => { ... });
```

Propagate `ct` to every async call.

## Result Integration

- Domain services may return `Result<T, VosCliException>` for recoverable operations
- Use `Result.FromTryAsync<T, TException>(...)` to wrap throwing operations
- Use `.Match(onSuccess, onFailure)` to branch — never ignore the failure branch
- Hard throw (via `VosCliThrow`) for unrecoverable state: missing config, missing entity, required option absent
- The top-level `catch (VosCliException)` in `Program.cs` is the last line of defense

## No Mediator, No Handler Interface

System.CommandLine v2 has no handler interface. Do not invent one. The command class IS the composition point. DI through the constructor, behavior through `SetAction`.
