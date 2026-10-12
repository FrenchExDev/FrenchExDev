# Builder — Claude Context

Source-generator-powered async object construction framework: validation-then-build, single-flight, cycle-safe via `Reference<T>` + `VisitedObjects`.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)
- [Comparison Table](doc/COMPARISON-TABLE.md)

## Relevant skills
- [BUILDER-PATTERN](../../../Skills/Net/Programming/BUILDER-PATTERN/PHILOSOPHY.md)
- [SG](../../../Skills/Net/Programming/SG/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.Builder.slnx`

## Notes for Claude
- Two distinct modes: `[Builder]` (Result-of-ValidationResult) and `[Builder(Exception=typeof(E))]` (Result<T,E>). Mode 2 requires the developer to implement abstract `InstantiateAsync` — the generator does not write it.
- The `Instantiate` bridge is **sealed**. Override `CreateInstance` (Mode 1) or `InstantiateAsync` (Mode 2), never the bridge.
- `BuildException` is sealed in Mode 2 because `netstandard2.0` lacks covariant returns; override `TypedBuildException` instead.
- Manual graph builders MUST call `reference.Resolve(instance)` before building children, and MUST thread `visitedObjects` into every nested `BuildAsync`. Skipping either rule produces infinite recursion.
- `VisitedObjects` is keyed by **builder instance** reference equality, not by built value (the value doesn't exist yet at cycle-detection time).
- `Validate*` hooks must `yield return` exception values, never `throw`. Throwing skips the rest of the validation pass.
- `BuilderEmitter` in `.SourceGenerator.Lib` is reused by other generators (BinaryWrapper, ComposeBundle, GitLab.DockerCompose, Diem). Customise via `Preamble`, `WithMethodAttributes`, `WithMethodBodyPrefix`, `InstantiationExpression` — never fork the emitter.
- Single-flight is automatic via `SemaphoreSlim(1,1)` + double-check. Do not add an external cache.
