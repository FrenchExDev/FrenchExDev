# SYSTEM.COMMANDLINE — Claude Context

System.CommandLine v2 architecture pattern: 4 class types — Symbols (static Option/Argument instances), Throw (named exceptions with `[DoesNotReturn]`), Resolver (ParseResult to strongly-typed input record), Command (inherits `Command`, DI constructor, `SetAction`). Reference implementation: Vos CLI.

## Skill docs
- [What](WHAT.md) — core concepts and architecture
- [How](HOW.md) — step-by-step guide
- [Rules](RULES.md) — constraints and conventions

## Related skills
- [RESULT-PATTERN](../RESULT-PATTERN/) — Result integration
- [SOLID](../SOLID/) — 1-method Resolver

## Related packages
- [`FrenchExDev.Net.Vos`](../../../../Net/FrenchExDev/Vos/) (reference implementation)

## Notes for Claude
- No `ICommandHandler` in v2 — behavior via `SetAction(async (ParseResult, CancellationToken) => ...)` at construction
- Object identity rule: same Option/Argument instance must be used in `Add` and `GetValue`
- No `!` (null-forgiving) operator — every nullable must be validated explicitly
- No silent failures — every error path is a named exception
- `SetAction` always takes `CancellationToken` (even if not used)
- Top-level `catch (CliException)` in Program.cs prints to stderr, exits 1
- Group commands have no action; they collect children via DI
