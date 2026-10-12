# Skills/Net/Programming — Index

Reusable, pattern-level skills used across the .NET monorepo. Each folder contains
exactly four Markdown files: `PHILOSOPHY.md`, `ARCHITECTURE.md`, `HOW-TO.md`,
`REQUIREMENTS.md`. No YAML frontmatter; pure prescriptive Markdown.

## Foundational

- [SOLUTION-LAYOUT](SOLUTION-LAYOUT/) — canonical .NET package directory shape
- [CENTRAL-PACKAGE-MANAGEMENT](CENTRAL-PACKAGE-MANAGEMENT/) — versions in `Directory.Packages.props`, never in `.csproj`
- [LOCAL-NUGET-REGISTRY](LOCAL-NUGET-REGISTRY/) — file-system NuGet feed for cross-package iteration

## Engineering principles

- [SOLID](SOLID/) — interface segregation, DI, OCP, SRP, LSP
- [CQRS](CQRS/) — commands vs. queries
- [DDD](DDD/) — aggregates, entities, value objects, invariants
- [REQUIREMENTS](REQUIREMENTS/) — formal requirements with traceability
- [QUALITY-GATES](QUALITY-GATES/) — coverage + mutation + complexity gates
- [DESIGN-PHASED-PROJECT](DESIGN-PHASED-PROJECT/) — `Design` sub-project for design-time codegen
- [HAND-WRITTEN-FAKES](HAND-WRITTEN-FAKES/) — test doubles without mocking frameworks

## Code generation

- [SG](SG/) — incremental Roslyn source generators
- [BUILDER-PATTERN](BUILDER-PATTERN/) — `AbstractBuilder<T>` + `[Builder]` SG
- [DSL-FOUNDATIONS](DSL-FOUNDATIONS/) — M3 metamodel, `[MetaConcept]`, behavioral companions
- [ENTITY-DSL](ENTITY-DSL/) — attribute-driven EF Core entities
- [INJECTABLE-DI](INJECTABLE-DI/) — `[Injectable]` for compile-time DI registration
- [FINITE-STATE-MACHINE](FINITE-STATE-MACHINE/) — three-tier (Dynamic / Typed / Rich) FSM library
- [IEC-61499](IEC-61499/) — function-block model for industrial control systems

## Core libraries

- [RESULT-PATTERN](RESULT-PATTERN/) — `Result`, `Result<T>`, `Result<T,TError>`
- [GUARD-CLAUSES](GUARD-CLAUSES/) — early-return preconditions
- [CLOCK-ABSTRACTION](CLOCK-ABSTRACTION/) — `IClock` + `FakeClock`
- [OPTIONS-PATTERN](OPTIONS-PATTERN/) — `Option<T>` discriminated absence
- [MAPPER-PATTERN](MAPPER-PATTERN/) — typed `IMapper<TSource, TTarget>` via SG
- [MEDIATOR-PATTERN](MEDIATOR-PATTERN/) — request/handler/behavior/notification contracts
- [OUTBOX-PATTERN](OUTBOX-PATTERN/) — transactional event publication via EF Core interceptor
- [SAGA-PATTERN](SAGA-PATTERN/) — orchestration + choreography with compensation
- [UNION-TYPES](UNION-TYPES/) — discriminated unions via sealed records
- [REACTIVE](REACTIVE/) — `IEventStream<T>` over `System.Reactive`

## CLI tooling

- [SYSTEM.COMMANDLINE](SYSTEM.COMMANDLINE/) — `System.CommandLine` conventions
- [BINARY-WRAPPER](BINARY-WRAPPER/) — typed CLI wrappers from `--help` scraping
- [WRAPPER-VERSIONING](WRAPPER-VERSIONING/) — version-discovery + design-time pipeline

## Compose / orchestration

- [COMPOSE-BUNDLE](COMPOSE-BUNDLE/) — schema-driven Docker Compose code generation
- [PACKER-BUNDLE](PACKER-BUNDLE/) — HCL2-first Packer template orchestration
- [VOS-ORCHESTRATION](VOS-ORCHESTRATION/) — typed VM orchestration over Vagrant + Podman Machine

## Apps / verticals

- [DIEM-CMF](DIEM-CMF/) — content management framework via composed sub-DSLs
