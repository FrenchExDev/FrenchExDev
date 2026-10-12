# BINARY-WRAPPER — Philosophy

A *binary wrapper* is a type-safe .NET facade over an external command-line tool. The pattern eliminates string-typed argument construction and `Process.Start` boilerplate by deriving an entire fluent API from the binary's own `--help` output. Apply this pattern when wrapping any CLI you call from .NET more than a few times.

## The Three Phases Are Non-Negotiable

A binary wrapper has exactly three phases. Conflating them is the most common design mistake.

1. **Design time** — scrape `--help` text from N versions of the binary into JSON command trees. Runs **manually**, not on every build. Slow (containers, network), produces durable artifacts checked into the repo.
2. **Build time** — a Roslyn incremental source generator reads those JSON files as `AdditionalFiles`, merges them across versions, and emits typed C# command classes, fluent builders, and a client. Runs on every build, must be deterministic and offline.
3. **Run time** — generated client builds an `ICliCommand`, an executor spawns the process, and an event-driven pipeline streams `OutputLine` → `IOutputParser<TEvent>` → `IResultCollector<TEvent, TResult>`. Zero reflection, zero string parsing in user code.

The boundary between phases is a serialized JSON file. This is what makes the wrapper reproducible: regenerating the C# from checked-in JSON requires no network, no container runtime, no installed binary.

## Help Text Is the Source of Truth

Do not hand-write command models. Do not maintain TypeScript-style type definitions. The binary itself, via `--help`, is canonical. Whatever the binary advertises is what the wrapper exposes — no more, no less.

This implies:

- **Every command** is discovered by recursively running `<binary> <subpath> --help` and parsing the output.
- **Every option** comes from a `Flags:` / `Options:` section, never from documentation or guesswork.
- **Every type hint** (`string`, `int`, `stringSlice`, etc.) is parsed from the help line, not invented.
- A custom `IHelpParser` exists per CLI framework family (Cobra, GNU getopt, argparse, HashiCorp, Vagrant). New frameworks plug in by implementing one interface.

When the binary lies (Vagrant 2.4.4–2.4.5 crashes on `vagrant box -h` due to a `server_mode?` Ruby bug), the wrapper records the gap rather than papering over it. Skipped or broken commands are tracked explicitly in a `SkippedCommands` set, not silently absorbed.

## Multi-Version Is Built In, Not Bolted On

A wrapper that targets only one version is a leaky abstraction the day a user upgrades. The pattern requires multi-version awareness from day one:

- Scrape **N JSON files**, one per supported version.
- A **VersionDiffer** merges them into a single unified command tree, attaching `[SinceVersion("x.y.z")]` and `[UntilVersion("x.y.z")]` to every command, option, and argument.
- A runtime **VersionGuard** throws `CommandNotSupportedException` / `OptionNotSupportedException` when the detected binary version is outside the supported range.

The user gets a single API surface that spans every version of the tool, with compile-time discovery and runtime safety. They never have to ask "is this flag in 1.9?" — the type system answers, and the runtime enforces.

## Source Generation Has Zero Runtime Cost

Everything the wrapper does at runtime is hand-written code that happens to have been emitted by a generator. There is:

- **No reflection** over command objects.
- **No dictionary-based dispatch** of "what flag does this command take".
- **No string parsing** in the hot path.

`ToArguments()` is a hand-rolled `string[]` builder. The fluent builder is an `AbstractBuilder<TCommand>` subclass with explicit `With*()` methods. The client is a sealed class with named methods per command. This makes the wrapper indistinguishable in performance from a hand-written one — but with the maintenance cost of a JSON file refresh.

## Naming Collisions Are Resolved at the Emitter, Not at the User

Real CLIs have ugly edges:

- `--no-tty` and `--[no-]tty` PascalCase to the same identifier → deduplicate at emit time.
- A `version` leaf command and a `version` sub-group both exist in some Podman versions → prune the leaf.
- An option `--class` clashes with a C# reserved word → escape with `@`.
- An option name contains `[]` or spaces → strip in `NamingHelper.ToPascalCase`.

These are **emitter responsibilities**, not user responsibilities. The user should never see `Class_` or `NoTty1`. Every collision rule lives in one place (`NamingHelper`, `CommandClassEmitter`, `ClientClassEmitter.PruneClashingLeaves`) and is exercised by tests against real scraped JSON.

## Two-Phase Scraping Beats One-Phase Every Time

Phase 1 builds a per-version container image (`<tool>-scrape:<version>`) that contains the binary. Phase 2 spins up a container from that image and executes `<binary> <subpath> --help` recursively. The image is the cache.

This separation matters because:

- Phase 1 is **slow and parallel-hostile** (network downloads, package installs).
- Phase 2 is **fast and parallel-friendly** (just exec calls inside a pre-built container).
- Re-running with `--missing` only fetches what's new.
- Re-running with `--reparse` skips containers entirely and reparses cached `.help.txt` dumps with an updated parser.

A one-phase pipeline (build + scrape in one shot) couples them and forces a full rebuild whenever the parser changes. The two-phase split makes parser iteration cheap.

## Parsers Are Plugins, Not Inheritance Trees

`IHelpParser.Parse(helpText, commandName)` returns a `CommandNode?`. That is the entire interface. New CLI frameworks plug in by implementing it. The framework ships:

- `StandardHelpParser` — generic GNU `Options:` / `Commands:`
- `CobraHelpParser` — Go/Cobra `Available Commands:` / `Flags:` with type hints
- `ArgparseHelpParser` — Python `positional arguments:` / `options:`
- `PackerHelpParser` — HashiCorp tools
- `VagrantHelpParser` — Vagrant's quirky `Common commands:` / `Available subcommands:`
- A `LoggingHelpParser` decorator that wraps any parser for debugging

There is no abstract base class to inherit from. There is no virtual method to override. The parser is a single function. This is what makes adding a new wrapper for a new tool a 1–2 day job, not a 1–2 week project.
