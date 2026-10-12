# System.CommandLine v2 — Concepts & Architecture

## Core Types

| Type | Role |
|------|------|
| `RootCommand` | Entry point. One per CLI tool. |
| `Command` | A verb in the command tree (`halt`, `snapshot save`). |
| `Option<T>` | Named flag (`--config`, `--force`). Matched by **object reference**, not by name. |
| `Argument<T>` | Positional value (`name`, `path`). Also matched by reference. |
| `ParseResult` | The parsed output. Access values via `GetValue(option)` / `GetValue(argument)`. |

## The v2 Action Model

System.CommandLine v2 has **no** `ICommandHandler` interface. The only way to attach behavior is:

```csharp
command.SetAction(async (ParseResult pr, CancellationToken ct) =>
{
    // ...
});
```

The delegate is set at construction time. DI works through the constructor of the command class, not through handler resolution.

## Object Identity Rule

`Option<T>` and `Argument<T>` are matched by **reference identity**, not by alias string.

The same instance must be used when:
1. Attaching to a command: `command.Options.Add(option)`
2. Reading the parsed value: `parseResult.GetValue(option)`

If you create two `Option<string>("--config")` instances, they are **different symbols**. Attaching one and reading the other yields `null`.

This is why all symbols live in a single static class — one instance per symbol, shared everywhere.

## Architecture — 4 Class Types

```
  VosCliSymbols          VosCliThrow           HaltCommandResolver        HaltCommand
  (static)               (static)              (DI-injected)              (DI-injected, : Command)
  +--------------+       +--------------+      +-------------------+      +------------------+
  | Config       |       | ConfigNot    |      | Resolve(pr)       |      | ctor(resolver,   |
  | Force        |       |   Found()    |      |  -> HaltInput     |      |      factory)    |
  | Name         |       | MachineType  |      |  validates, throws|      | SetAction(...)   |
  | SnapshotName |       |   NotFound() |      +-------------------+      +------------------+
  | ...          |       | Required     |
  +--------------+       |   Option     |
                         |   Missing()  |
                         | ...          |
                         +--------------+
```

### 1. Symbols (static class)

Owns all `Option<T>` and `Argument<T>` instances. One class per CLI tool. Stateless — no DI needed.

```csharp
public static class VosCliSymbols
{
    public static Option<string> Config { get; } = new("--config") { ... };
    public static Argument<string> Name { get; } = new("name") { ... };
}
```

Options that appear on many commands ("inherited") are simply referenced from multiple command constructors — same instance, attached to different `Command` objects.

### 2. Exceptions (hierarchy + static throw class)

Every distinct failure mode is a **named, sealed exception**. A static `Throw` class provides `[DoesNotReturn]` factory methods.

```csharp
public abstract class VosCliException(string message) : Exception(message);

public sealed class ConfigNotFoundException(string path)
    : VosCliException($"Config not found: '{path}'.");
```

```csharp
public static class VosCliThrow
{
    [DoesNotReturn]
    public static void ConfigNotFound(string path)
        => throw new ConfigNotFoundException(path);
}
```

Why named exceptions:
- `catch (ConfigNotFoundException)` is precise; `catch (Exception) when (e.Message.Contains(...))` is fragile
- `Result<T, VosCliException>` carries the error type, not just a string
- The exception list **is** the specification of what can go wrong

### 3. Resolver (DI-injected)

Bridges `ParseResult` to a strongly-typed input record. **Validates every nullable value and throws if absent.** No `!` operator, no silent `return`.

```csharp
public record HaltInput(string Name, string ConfigPath, bool Force);

public class HaltCommandResolver
{
    public HaltInput Resolve(ParseResult pr)
    {
        var name = pr.GetValue(VosCliSymbols.Name)
            ?? throw new RequiredOptionMissingException("name");
        var config = pr.GetValue(VosCliSymbols.Config)
            ?? throw new RequiredOptionMissingException("--config");
        return new HaltInput(name, config, pr.GetValue(VosCliSymbols.Force));
    }
}
```

`ParseResult` never leaks past the resolver. Domain code receives the input record, nothing else.

### 4. Command (DI-injected, inherits `Command`)

Each command is its own class. Constructor receives DI dependencies (resolver, services). Attaches symbols and sets the action.

```csharp
public class HaltCommand : Command
{
    public HaltCommand(HaltCommandResolver resolver, VosConfigService configService)
        : base("halt", "Stop VM(s)")
    {
        Arguments.Add(VosCliSymbols.Name);
        Options.Add(VosCliSymbols.Config);
        Options.Add(VosCliSymbols.Force);

        SetAction(async (pr, ct) =>
        {
            var input = resolver.Resolve(pr);
            var config = await configService.LoadOrThrowAsync(input.ConfigPath);
            // ...
        });
    }
}
```

Group commands (containers) have no action — they just collect children via DI:

```csharp
public class SnapshotGroupCommand : Command
{
    public SnapshotGroupCommand(
        SnapshotSaveCommand save,
        SnapshotRestoreCommand restore,
        SnapshotListCommand list)
        : base("snapshot", "Manage VM snapshots")
    {
        Subcommands.Add(save);
        Subcommands.Add(restore);
        Subcommands.Add(list);
    }
}
```

## Error Philosophy

- **No `!`** — every nullable is validated, throw if null
- **No `if (x is null) return`** — silent failures are bugs in a dev CLI
- **Every failure is a named exception** — not a string message on `InvalidOperationException`
- **`Result<T, VosCliException>`** for recoverable operations (services that may fail)
- **Hard throw** for unrecoverable state (missing config, missing entity, required option absent)
- **Top-level catch** in `Program.cs` catches the base exception, prints to stderr, exits 1

## Integration with Result

`Result<TResult, TError>` where `TError : Exception` works naturally:

- `Result.FromTryAsync<T, ConfigParseException>(...)` — wraps a throwing operation into a Result
- `.Match(onSuccess, onFailure)` — branch on outcome
- `.ValueOrThrow()` — unwrap or throw `InvalidOperationException`
- The error type carries the full exception — message, inner exception, structured data (e.g., `ConfigValidationException.Errors`)
