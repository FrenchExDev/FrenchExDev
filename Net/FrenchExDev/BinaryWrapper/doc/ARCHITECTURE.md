# BinaryWrapper Architecture

BinaryWrapper is a framework for generating type-safe .NET wrappers around CLI binaries. It scrapes `--help` output across multiple versions, generates versioned C# code via Roslyn source generators, and provides a runtime execution pipeline with structured output parsing.

---

## System Overview

```mermaid
flowchart TB
    subgraph DT["DESIGN TIME"]
        direction TB
        VC["IVersionCollector<br/><i>GitHubReleasesVersionCollector</i><br/><i>StaticVersionCollector</i>"]
        MVS["MultiVersionScraper<br/><i>parallel Channel&lt;T&gt; workers</i>"]
        SP["ScrapePipeline<br/><i>fluent configuration</i>"]
        HS["HelpScraper<br/><i>recursive depth-first</i>"]
        HP["IHelpParser<br/><i>StandardHelpParser</i><br/><i>PackerHelpParser</i><br/><i>VagrantHelpParser</i>"]
        CR["IContainerRuntime<br/><i>PodmanContainerRuntime</i><br/><i>DockerContainerRuntime</i>"]
        JSON["JSON Command Trees<br/><i>binary-{version}.json</i>"]

        VC -->|"versions"| MVS
        MVS -->|"per version"| SP
        SP -->|"configures"| HS
        HS -->|"recursive --help"| HP
        HS -.->|"run inside"| CR
        HP -->|"CommandNode"| HS
        SP -->|"serialize"| JSON
    end

    subgraph BT["BUILD TIME"]
        direction TB
        DESC["[BinaryWrapper] Descriptor<br/><i>partial class</i>"]
        CTR["CommandTreeReader<br/><i>JSON → CommandTreeModel</i>"]
        VD["VersionDiffer<br/><i>Merge / FromSingle</i>"]
        UCT["UnifiedCommandTree<br/><i>version-annotated</i>"]
        CCE["CommandClassEmitter"]
        BCE["BuilderClassEmitter"]
        CLE["ClientClassEmitter"]

        JSON -->|"AdditionalFiles"| CTR
        DESC -->|"triggers"| CTR
        CTR -->|"per-version trees"| VD
        VD --> UCT
        UCT --> CCE
        UCT --> BCE
        UCT --> CLE
    end

    subgraph RT["RUNTIME"]
        direction TB
        CLIENT["Generated Client<br/><i>{Binary}Client</i>"]
        BUILDER["Generated Builder<br/><i>AbstractBuilder&lt;TCommand&gt;</i>"]
        CMD["Generated Command<br/><i>ICliCommand</i>"]
        VG["VersionGuard"]
        BR["IBinaryResolver<br/><i>DictionaryBinaryResolver</i>"]
        BB["BinaryBinding<br/><i>path + version + overrides</i>"]
        EXEC["CommandExecutor"]
        PR["IProcessRunner<br/><i>SystemProcessRunner</i>"]
        OP["IOutputParser&lt;TEvent&gt;"]
        RC["IResultCollector&lt;TEvent, TResult&gt;"]
        RESULT["TResult"]

        CLIENT -->|"configure"| BUILDER
        BUILDER -->|"Build()"| CMD
        BUILDER -.->|"validates"| VG
        CLIENT -.->|"validates"| VG
        BR -->|"resolve"| BB
        CMD --> EXEC
        BB --> EXEC
        EXEC -->|"spawn"| PR
        PR -->|"OutputLine stream"| OP
        OP -->|"TEvent stream"| RC
        RC --> RESULT
    end

    CCE -->|"generates"| CMD
    BCE -->|"generates"| BUILDER
    CLE -->|"generates"| CLIENT

    style DT fill:#1a1a2e,stroke:#e94560,color:#eee
    style BT fill:#16213e,stroke:#0f3460,color:#eee
    style RT fill:#0f3460,stroke:#533483,color:#eee
```

---

## Package Architecture

```mermaid
graph TD
    subgraph Consumer["Consumer Project (e.g. Vagrant)"]
        LIB["FrenchExDev.Net.Vagrant<br/><i>Library + Generated Code</i>"]
        DESIGN["FrenchExDev.Net.Vagrant.Design<br/><i>Scraping Exe</i>"]
        TESTS["FrenchExDev.Net.Vagrant.Tests"]
    end

    subgraph Framework["BinaryWrapper Framework"]
        CORE["FrenchExDev.Net.BinaryWrapper<br/><i>Core Runtime (net10.0)</i>"]
        ATTR["FrenchExDev.Net.BinaryWrapper.Attributes<br/><i>[BinaryWrapper] (netstandard2.0)</i>"]
        SG["FrenchExDev.Net.BinaryWrapper.SourceGenerator<br/><i>Roslyn 4.3.1 (netstandard2.0)</i>"]
        DES["FrenchExDev.Net.BinaryWrapper.Design<br/><i>CLI Tool (net10.0)</i>"]
        TEST["FrenchExDev.Net.BinaryWrapper.Testing<br/><i>Test Helpers (net10.0)</i>"]
    end

    subgraph Foundation["Foundation"]
        RES["FrenchExDev.Net.Result<br/><i>Result&lt;T&gt;, Result&lt;T,TError&gt;</i>"]
        BLD["FrenchExDev.Net.Builder<br/><i>AbstractBuilder&lt;T&gt;</i>"]
    end

    LIB -->|"runtime"| CORE
    LIB -->|"attribute"| ATTR
    LIB -->|"analyzer"| SG
    LIB --> RES
    LIB --> BLD
    DESIGN -->|"uses"| DES
    TESTS -->|"uses"| TEST

    CORE --> RES
    SG -.->|"generates code using"| CORE
    SG -.->|"generates code using"| BLD
    TEST --> CORE
    TEST --> DES

    style Consumer fill:#2d3436,stroke:#636e72,color:#eee
    style Framework fill:#1a1a2e,stroke:#e94560,color:#eee
    style Foundation fill:#0f3460,stroke:#533483,color:#eee
```

---

## Package Details

### FrenchExDev.Net.BinaryWrapper (Core Runtime)

The runtime library that consumers depend on. Contains all abstractions for command execution, binary resolution, version management, and output parsing.

```mermaid
classDiagram
    class ICliCommand {
        <<interface>>
        +CommandPath IReadOnlyList~string~
        +ToArguments() IReadOnlyList~string~
    }

    class BinaryIdentifier {
        +Name string
        +VersionConstraint string?
    }

    class BinaryBinding {
        +Identifier BinaryIdentifier
        +ExecutablePath string
        +DetectedVersion SemanticVersion?
        +EnvironmentVariables IDictionary?
        +Overrides CommandOverrides?
    }

    class CommandOverrides {
        +OptionNameMappings IDictionary~string,string~
        +UnsupportedOptions ISet~string~
    }

    class IBinaryResolver {
        <<interface>>
        +ResolveAsync(BinaryIdentifier) Task~Result~BinaryBinding~~
    }

    class DictionaryBinaryResolver {
        +Add(BinaryBinding)
    }

    class SemanticVersion {
        +Major int
        +Minor int
        +Patch int
        +Prerelease string?
        +Metadata string?
        +Parse(string)$ SemanticVersion
        +TryParse(string)$ SemanticVersion?
    }

    class IVersionDetector {
        <<interface>>
        +DetectAsync(string executablePath) Task~SemanticVersion?~
    }

    class VersionGuard {
        +EnsureCommandSupported(version, path, since, until)$
        +EnsureOptionSupported(version, path, option, since, until)$
    }

    class CommandExecutor {
        +ExecuteAsync(binding, command) Task~ProcessOutput~
        +StreamAsync~TEvent~(binding, command, parser) IAsyncEnumerable~TEvent~
        +ExecuteAsync~TEvent,TResult~(binding, command, parser, collector) Task~TResult~
    }

    class IProcessRunner {
        <<interface>>
        +StreamAsync(ProcessSpec) IAsyncEnumerable~OutputLine~
    }

    class IOutputParser~TEvent~ {
        <<interface>>
        +ParseLine(OutputLine) IEnumerable~TEvent~
        +Complete() IEnumerable~TEvent~
    }

    class IResultCollector~TEvent_TResult~ {
        <<interface>>
        +OnEvent(TEvent)
        +Complete() TResult
    }

    class OutputLine {
        +Text string
        +Source OutputSource
    }

    class ProcessSpec {
        +ExecutablePath string
        +Arguments IReadOnlyList~string~
        +EnvironmentVariables IDictionary?
        +Timeout TimeSpan?
    }

    IBinaryResolver <|.. DictionaryBinaryResolver
    IBinaryResolver --> BinaryBinding
    BinaryBinding --> BinaryIdentifier
    BinaryBinding --> SemanticVersion
    BinaryBinding --> CommandOverrides
    CommandExecutor --> IBinaryResolver
    CommandExecutor --> IProcessRunner
    CommandExecutor --> ICliCommand
    IProcessRunner --> OutputLine
    IProcessRunner --> ProcessSpec
    CommandExecutor ..> IOutputParser~TEvent~
    CommandExecutor ..> IResultCollector~TEvent_TResult~
    IVersionDetector --> SemanticVersion
```

#### Command Execution Pipeline

```mermaid
sequenceDiagram
    participant App as Application
    participant Client as Generated Client
    participant Builder as Generated Builder
    participant VG as VersionGuard
    participant Executor as CommandExecutor
    participant Resolver as IBinaryResolver
    participant Runner as IProcessRunner
    participant Parser as IOutputParser
    participant Collector as IResultCollector

    App->>Client: client.Build(b => b.WithForce(true))
    Client->>VG: EnsureCommandSupported(version, path)
    Client->>Builder: new Builder(detectedVersion)
    App->>Builder: .WithForce(true)
    Builder->>VG: EnsureOptionSupported(version, "force")
    Builder->>Builder: BuildAsync()
    Builder-->>Client: Command instance

    App->>Executor: ExecuteAsync(binding, command, parser, collector)
    Executor->>Resolver: ResolveAsync(identifier)
    Resolver-->>Executor: BinaryBinding

    Executor->>Executor: BuildProcessSpec(binding, command.ToArguments())
    Note over Executor: Apply CommandOverrides<br/>(option mappings, unsupported options)

    Executor->>Runner: StreamAsync(processSpec)
    loop Each output line
        Runner-->>Executor: OutputLine
        Executor->>Parser: ParseLine(outputLine)
        Parser-->>Executor: TEvent[]
        loop Each event
            Executor->>Collector: OnEvent(event)
        end
    end
    Executor->>Parser: Complete()
    Parser-->>Executor: final TEvent[]
    Executor->>Collector: Complete()
    Collector-->>App: TResult
```

#### Error Hierarchy

```mermaid
classDiagram
    class BinaryResolutionError {
        +Identifier BinaryIdentifier
        +Message string
    }
    class CommandError {
        +ExitCode int
        +Stderr string
        +Message string
    }
    class CommandNotSupportedException {
        +CommandPath string[]
        +DetectedVersion SemanticVersion
        +RequiredVersion string
    }
    class OptionNotSupportedException {
        +OptionName string
        +CommandPath string[]
        +DetectedVersion SemanticVersion
        +RequiredVersion string
    }

    Exception <|-- CommandNotSupportedException
    Exception <|-- OptionNotSupportedException
```

#### Version Constraints

```mermaid
flowchart LR
    subgraph Versions["Binary Versions Available"]
        V1["1.9.0"]
        V2["1.10.0"]
        V3["1.11.0"]
        V4["1.12.0"]
    end

    subgraph Command["PackerBuildCommand"]
        O1["--force<br/><i>all versions</i>"]
        O2["--validate<br/><i>[SinceVersion 1.10.0]</i>"]
        O3["--legacy-flag<br/><i>[UntilVersion 1.11.0]</i>"]
        O4["--ignore-prerelease<br/><i>[SinceVersion 1.11.0]</i>"]
    end

    subgraph Runtime["Runtime Check"]
        DV["DetectedVersion = 1.10.0"]
        DV -->|"--force"| OK1["OK"]
        DV -->|"--validate"| OK2["OK (>= 1.10)"]
        DV -->|"--legacy-flag"| OK3["OK (< 1.11)"]
        DV -->|"--ignore-prerelease"| FAIL["OptionNotSupportedException<br/><i>requires 1.11.0</i>"]
    end

    style FAIL fill:#c0392b,stroke:#e74c3c,color:#fff
    style OK1 fill:#27ae60,stroke:#2ecc71,color:#fff
    style OK2 fill:#27ae60,stroke:#2ecc71,color:#fff
    style OK3 fill:#27ae60,stroke:#2ecc71,color:#fff
```

---

### FrenchExDev.Net.BinaryWrapper.Attributes

Tiny package (netstandard2.0 + net10.0) containing only the `[BinaryWrapper]` attribute:

```csharp
[BinaryWrapper("packer", FlagPrefix = "-", FlagValueSeparator = "=", UseBoolEqualsFormat = true)]
public partial class PackerDescriptor;
```

| Property | Default | Description |
|----------|---------|-------------|
| `BinaryName` | *(required)* | Binary name, used to match `{name}-*.json` AdditionalFiles |
| `FlagPrefix` | `"--"` | GNU-style: `"--"`, Go-style: `"-"` |
| `FlagValueSeparator` | `" "` | Space: `--flag value`, Equals: `--flag=value` |
| `UseBoolEqualsFormat` | `false` | `true` → `-force=true`/`-force=false`, `false` → `--force` (presence) |

---

### FrenchExDev.Net.BinaryWrapper.SourceGenerator

Roslyn 4.3.1 incremental source generator. Triggered by `[BinaryWrapper]` on a partial class.

#### Generation Pipeline

```mermaid
flowchart TD
    subgraph Input
        DESC["[BinaryWrapper('packer')]<br/>partial class PackerDescriptor"]
        AF1["packer-1.9.0.json"]
        AF2["packer-1.10.0.json"]
        AF3["packer-1.11.0.json"]
    end

    subgraph Generator["BinaryWrapperGenerator.Initialize()"]
        S1["1. Find [BinaryWrapper]<br/>descriptors"]
        S2["2. Match AdditionalFiles<br/>by binary name pattern"]
        S3["3. CommandTreeReader.Parse()<br/>extract version from filename"]
        S4["4. VersionDiffer.Merge()<br/>compute since/until annotations"]
        S5["5. Emit generated source"]

        S1 --> S2 --> S3 --> S4 --> S5
    end

    subgraph Output["Generated Files"]
        M["PackerDescriptor.BinaryWrapper.g.cs<br/><i>partial class with constants</i>"]
        C1["PackerBuildCommand.g.cs<br/><i>sealed ICliCommand</i>"]
        C2["PackerValidateCommand.g.cs"]
        B1["PackerBuildCommandBuilder.g.cs<br/><i>AbstractBuilder&lt;T&gt;</i>"]
        B2["PackerValidateCommandBuilder.g.cs"]
        CL["PackerClient.g.cs<br/><i>typed client + entry point</i>"]
    end

    DESC --> S1
    AF1 --> S2
    AF2 --> S2
    AF3 --> S2
    S5 --> M
    S5 --> C1
    S5 --> C2
    S5 --> B1
    S5 --> B2
    S5 --> CL

    style Input fill:#1a1a2e,stroke:#e94560,color:#eee
    style Generator fill:#16213e,stroke:#0f3460,color:#eee
    style Output fill:#0f3460,stroke:#533483,color:#eee
```

#### Emitter Responsibilities

| Emitter | Output | Details |
|---------|--------|---------|
| `CommandClassEmitter` | `{Binary}{Cmd}Command.g.cs` | Sealed class implementing `ICliCommand`. Init-only properties for each option/argument. `ToArguments()` serializes using the configured flag prefix and separator. |
| `BuilderClassEmitter` | `{Binary}{Cmd}CommandBuilder.g.cs` | Extends `AbstractBuilder<T>`. Fluent `With{Option}()` methods with `VersionGuard` checks. Virtual `Validate{Option}()` for customization. Sealed `Instantiate` override. |
| `ClientClassEmitter` | `{Binary}Client.g.cs` | Static `{Binary}.Create(binding)` entry point. Nested groups for command hierarchy (e.g., `client.Container.Run(...)`). `PruneClashingLeaves` handles version-compatibility edge cases. |

#### Key Source Generator Types

| Type | Purpose |
|------|---------|
| `CommandTreeReader` | Deserializes JSON, extracts version from `{binary}-{version}.json` filename |
| `NamingHelper` | kebab-case → PascalCase, CLR type mapping, deduplication of options with same PascalCase name |
| `VersionDiffer` | Produces `UnifiedCommandTree` with per-command and per-option version annotations |

#### Generated Code Shape

```mermaid
classDiagram
    class ICliCommand {
        <<interface>>
    }

    class PackerBuildCommand {
        <<sealed>>
        +Force bool?
        +ParallelBuilds int?
        +Var IReadOnlyList~string~?
        +Template string?
        +CommandPath IReadOnlyList~string~
        +ToArguments() IReadOnlyList~string~
    }

    class PackerBuildCommandBuilder {
        -_detectedVersion SemanticVersion?
        +WithForce(bool) PackerBuildCommandBuilder
        +WithParallelBuilds(int) PackerBuildCommandBuilder
        +WithVar(IReadOnlyList~string~) PackerBuildCommandBuilder
        +WithTemplate(string) PackerBuildCommandBuilder
        #ValidateForce(bool?) IEnumerable~Exception~?
        #ValidateTemplate(string?) IEnumerable~Exception~?
        #ValidateAsync() Task~Result~ValidationResult~~
        #Instantiate() Task~Result~Reference~PackerBuildCommand~~~
    }

    class PackerClient {
        -_binding BinaryBinding
        -_detectedVersion SemanticVersion?
        +Build(Action~PackerBuildCommandBuilder~) PackerBuildCommand
        +Validate(Action~PackerValidateCommandBuilder~) PackerValidateCommand
        +Plugins PackerClientPluginsGroup
    }

    class PackerClientPluginsGroup {
        -_client PackerClient
        +Install(Action~...~) PackerPluginsInstallCommand
        +Remove(Action~...~) PackerPluginsRemoveCommand
    }

    class Packer {
        +Create(BinaryBinding)$ PackerClient
    }

    ICliCommand <|.. PackerBuildCommand
    AbstractBuilder~PackerBuildCommand~ <|-- PackerBuildCommandBuilder
    PackerBuildCommandBuilder ..> PackerBuildCommand : creates
    PackerClient --> PackerBuildCommandBuilder : uses
    PackerClient --> PackerClientPluginsGroup : nested group
    Packer --> PackerClient : creates
```

#### Nested Command Groups

For CLI tools with sub-command hierarchies (e.g., `vagrant box add`, `podman container run`):

```mermaid
flowchart TD
    Client["VagrantClient"]
    Client --> Up["Up(configure)"]
    Client --> Halt["Halt(configure)"]
    Client --> Box["BoxGroup"]
    Client --> Plugin["PluginGroup"]

    Box --> BoxAdd["Add(configure)"]
    Box --> BoxList["List(configure)"]
    Box --> BoxRemove["Remove(configure)"]

    Plugin --> PluginInstall["Install(configure)"]
    Plugin --> PluginList["List(configure)"]
    Plugin --> PluginRepair["Repair(configure)"]

    style Client fill:#2d3436,stroke:#636e72,color:#eee
    style Box fill:#16213e,stroke:#0f3460,color:#eee
    style Plugin fill:#16213e,stroke:#0f3460,color:#eee
```

Each group is generated as a nested partial class:

```csharp
public partial class VagrantClient
{
    public VagrantClientBoxGroup Box => new(_client: this);

    public partial class VagrantClientBoxGroup
    {
        private readonly VagrantClient _client;

        public VagrantBoxAddCommand Add(Action<VagrantBoxAddCommandBuilder> configure) { ... }
        public VagrantBoxListCommand List(Action<VagrantBoxListCommandBuilder> configure) { ... }
    }
}
```

#### Diagnostics

| Code | Severity | Description |
|------|----------|-------------|
| `BW001` | Warning | No JSON help files found matching binary name |
| `BW002` | Warning | JSON parse error in AdditionalFile |
| `BW004` | Error | Descriptor class is not partial |

---

### FrenchExDev.Net.BinaryWrapper.Design

Design-time CLI tool packaged as a NuGet tool (`ToolCommandName: binary-wrapper`).

#### Scraping Architecture

```mermaid
flowchart TD
    subgraph Orchestration
        CLI["CLI Entry Point<br/><i>new / scrape-all</i>"]
        MVS["MultiVersionScraper"]
        CH["Channel&lt;string&gt;<br/><i>version queue</i>"]
        W1["Worker 1"]
        W2["Worker 2"]
        W3["Worker N"]
    end

    subgraph VersionDiscovery["Version Discovery"]
        IVC["IVersionCollector"]
        GH["GitHubReleasesVersionCollector<br/><i>paginated API, tag filtering</i>"]
        ST["StaticVersionCollector<br/><i>fixed list</i>"]
    end

    subgraph Pipeline["Per-Version Pipeline"]
        SP["ScrapePipeline"]
        HS["HelpScraper"]
        RH["WithRunHelp callback<br/><i>local / container / SSH</i>"]
        HP["IHelpParser"]
        TX["ICommandTreeTransformer<br/><i>post-scrape transforms</i>"]
        SER["CommandTreeJsonSerializer"]
    end

    subgraph Container["Container Runtime (optional)"]
        ICR["IContainerRuntime"]
        POD["PodmanContainerRuntime"]
        DOC["DockerContainerRuntime"]
    end

    CLI --> MVS
    IVC --> MVS
    GH -.-> IVC
    ST -.-> IVC
    MVS --> CH
    CH --> W1
    CH --> W2
    CH --> W3
    W1 & W2 & W3 --> SP
    SP --> HS
    HS -->|"run binary --help"| RH
    RH -.->|"optionally"| ICR
    HS --> HP
    SP -->|"post-process"| TX
    SP --> SER
    SER -->|"write"| JSON["binary-{version}.json"]
    ICR --> POD
    ICR --> DOC

    style Orchestration fill:#1a1a2e,stroke:#e94560,color:#eee
    style Pipeline fill:#16213e,stroke:#0f3460,color:#eee
    style Container fill:#0f3460,stroke:#533483,color:#eee
```

#### Data Model

```mermaid
classDiagram
    class CommandTree {
        +BinaryName string
        +Version string?
        +Description string?
        +Root CommandNode
    }

    class CommandNode {
        +Name string
        +Description string?
        +Options IReadOnlyList~OptionDefinition~
        +Arguments IReadOnlyList~ArgumentDefinition~
        +SubCommands IReadOnlyList~CommandNode~
        +IsLeaf bool
        +GetLeafCommands() IEnumerable
        +FindByPath(segments) CommandNode?
    }

    class OptionDefinition {
        +LongName string
        +ShortName string?
        +Description string?
        +ValueKind OptionValueKind
        +ClrType string
        +DefaultValue string?
        +IsRequired bool
    }

    class ArgumentDefinition {
        +Name string
        +Position int
        +Description string?
        +ClrType string
        +IsRequired bool
        +IsVariadic bool
        +DefaultValue string?
    }

    class OptionValueKind {
        <<enumeration>>
        Flag
        Single
        Multiple
    }

    CommandTree --> CommandNode : root
    CommandNode --> CommandNode : subCommands
    CommandNode --> OptionDefinition : options
    CommandNode --> ArgumentDefinition : arguments
    OptionDefinition --> OptionValueKind
```

#### Option Types and CLI Serialization

| `OptionValueKind` | C# Type | CLI Serialization (GNU `--`) | CLI Serialization (Go `-`) |
|----|----|----|----|
| `Flag` | `bool?` | `--verbose` (presence = true) | `-verbose=true` / `-verbose=false` |
| `Single` | `string?`, `int?`, etc. | `--output value` or `--output=value` | `-output=value` |
| `Multiple` | `IReadOnlyList<T>?` | `--var val1 --var val2` (repeated) | `-var=val1 -var=val2` |

#### Help Parser Comparison

| Parser | Style | Headers | Flag Syntax | Use Case |
|--------|-------|---------|-------------|----------|
| `StandardHelpParser` | GNU | `Commands:`, `Options:`, `Arguments:` | `--flag`, `-f, --flag VALUE` | Most CLIs (podman, docker, git) |
| `PackerHelpParser` | Go | `Available commands:`, `Options:`, `Flags:` | `-flag`, `-flag=value` | HashiCorp tools (packer, terraform) |
| Custom (`VagrantHelpParser`) | Mixed | `Common commands:`, `Available subcommands:` | Varies | Any binary with non-standard help |

#### ScrapePipeline Fluent API

```csharp
new ScrapePipeline()
    .Binary("vagrant")                              // binary name
    .HelpFlag("-h")                                 // flag to trigger help
    .UseParser<VagrantHelpParser>()                  // or UseParser(instance)
    .MaxDepth(5)                                    // max recursion depth
    .WithRunHelp(async helpArgs => { ... })          // how to execute help
    .WithRuntime(new PodmanContainerRuntime())       // optional container runtime
    .FromImage("debian:bookworm")                   // base image for containers
    .Install("apt-get install -y vagrant")          // install command in container
    .TransformRoot(root => { ... })                 // post-scrape root transform
    .TransformCommand("box.add", node => { ... })   // transform specific command
    .UseTransformer(new MyTransformer())            // custom transformer
    .OutputTo("scrape/vagrant-2.4.3.json");         // output path
```

#### JSON Schema

```json
{
  "binaryName": "vagrant",
  "version": "2.4.3",
  "description": "Vagrant manages development environments",
  "root": {
    "name": "vagrant",
    "description": "...",
    "options": [],
    "arguments": [],
    "subCommands": [
      {
        "name": "box",
        "description": "Manage boxes",
        "options": [],
        "arguments": [],
        "subCommands": [
          {
            "name": "add",
            "description": "Add a box",
            "options": [
              {
                "longName": "force",
                "shortName": "f",
                "description": "Overwrite existing box",
                "valueKind": "flag",
                "clrType": "bool",
                "defaultValue": null,
                "isRequired": false
              },
              {
                "longName": "provider",
                "shortName": null,
                "description": "Provider for the box",
                "valueKind": "single",
                "clrType": "string",
                "defaultValue": null,
                "isRequired": false
              }
            ],
            "arguments": [
              {
                "name": "name",
                "position": 0,
                "description": "Name or URL of the box",
                "clrType": "string",
                "isRequired": true,
                "isVariadic": false,
                "defaultValue": null
              }
            ],
            "subCommands": []
          }
        ]
      }
    ]
  }
}
```

---

### FrenchExDev.Net.BinaryWrapper.Testing

Test helpers for consumers building binary wrappers.

```mermaid
classDiagram
    class FakeProcessRunner {
        +Lines IReadOnlyList~OutputLine~
        +LastSpec ProcessSpec?
        +StreamAsync(spec) IAsyncEnumerable~OutputLine~
    }

    class FakeCommand {
        +CommandPath IReadOnlyList~string~
        +Args IReadOnlyList~string~
        +ToArguments() IReadOnlyList~string~
    }

    class TestOutputParser {
        +ParseLine(OutputLine) IEnumerable~TestEvent~
        +Complete() IEnumerable~TestEvent~
    }

    class TestCollector {
        +OnEvent(TestEvent)
        +Complete() string
    }

    class TestBindings {
        +Create(name)$ BinaryBinding
        +Create(name, version)$ BinaryBinding
        +ResolverFor(name)$ DictionaryBinaryResolver
    }

    class Gens {
        +NonEmpty Gen~string~$
        +AnyBinaryId Gen~BinaryIdentifier~$
        +AnyOutputLine Gen~OutputLine~$
        +AnyVersion Gen~SemanticVersion~$
    }

    class MockContainerRuntime {
        +OnBuild Func?
        +OnRun Func?
        +OnRemove Func?
    }

    class FakeHttpHandler {
        +Responses Queue~HttpResponseMessage~
    }

    IProcessRunner <|.. FakeProcessRunner
    ICliCommand <|.. FakeCommand
    IOutputParser <|.. TestOutputParser
    IResultCollector <|.. TestCollector
    IContainerRuntime <|.. MockContainerRuntime
```

---

## Consumer Architecture

A consumer wraps a specific CLI binary. It consists of two main projects:

```mermaid
flowchart LR
    subgraph Consumer["Consumer Solution"]
        direction TB
        DESIGN["Design Project (Exe)<br/><i>Scraper + Version Collector</i>"]
        LIB["Library Project<br/><i>Descriptor + Generated Code + Parsers</i>"]
        TESTS["Test Project"]

        DESIGN -->|"produces JSON"| LIB
        TESTS -->|"tests"| LIB
    end

    subgraph Generated["Generated Code (by Source Generator)"]
        CMD["Command Classes<br/><i>sealed ICliCommand</i>"]
        BLD["Builder Classes<br/><i>AbstractBuilder&lt;T&gt;</i>"]
        CLT["Client + Entry Point<br/><i>nested groups</i>"]
    end

    subgraph HandWritten["Hand-Written Code"]
        DESC["Descriptor<br/><i>[BinaryWrapper('name')]</i>"]
        EVT["Event Records<br/><i>domain-specific events</i>"]
        PRS["Output Parser<br/><i>IOutputParser&lt;TEvent&gt;</i>"]
        COL["Result Collector<br/><i>IResultCollector&lt;TEvent,TResult&gt;</i>"]
    end

    LIB --> Generated
    LIB --> HandWritten

    style Consumer fill:#1a1a2e,stroke:#e94560,color:#eee
    style Generated fill:#16213e,stroke:#0f3460,color:#eee
    style HandWritten fill:#0f3460,stroke:#533483,color:#eee
```

---

## Version Compatibility Flow

```mermaid
flowchart TD
    subgraph Input["JSON Files"]
        J1["packer-1.9.0.json"]
        J2["packer-1.10.0.json"]
        J3["packer-1.11.0.json"]
        J4["packer-1.12.0.json"]
    end

    MERGE["VersionDiffer.Merge()"]

    subgraph Unified["UnifiedCommandTree"]
        CMD_B["build command<br/><i>all versions</i>"]
        CMD_V["validate command<br/><i>since 1.10.0</i>"]
        CMD_L["legacy command<br/><i>until 1.11.0</i>"]
        OPT_F["--force<br/><i>all versions</i>"]
        OPT_I["--ignore-prerelease<br/><i>since 1.11.0</i>"]
        OPT_D["--debug<br/><i>until 1.12.0</i>"]
    end

    subgraph Runtime["Runtime Enforcement"]
        DET["DetectedVersion = 1.10.0"]
        CHECK1["build → OK"]
        CHECK2["validate → OK (>= 1.10)"]
        CHECK3["legacy → OK (< 1.11)"]
        CHECK4["--ignore-prerelease → THROW<br/><i>requires >= 1.11.0</i>"]
    end

    J1 & J2 & J3 & J4 --> MERGE
    MERGE --> CMD_B & CMD_V & CMD_L
    CMD_B --> OPT_F & OPT_I & OPT_D

    DET --> CHECK1 & CHECK2 & CHECK3 & CHECK4

    style CHECK4 fill:#c0392b,stroke:#e74c3c,color:#fff
    style CHECK1 fill:#27ae60,stroke:#2ecc71,color:#fff
    style CHECK2 fill:#27ae60,stroke:#2ecc71,color:#fff
    style CHECK3 fill:#27ae60,stroke:#2ecc71,color:#fff
```

---

## Data Flow Summary

```mermaid
flowchart TD
    A["Binary help text<br/>(per version)"] -->|"HelpScraper + IHelpParser"| B["CommandTree<br/>(per-version JSON)"]
    B -->|"VersionDiffer.Merge"| C["UnifiedCommandTree<br/>(version-annotated)"]
    C -->|"Emitters"| D["Generated C# source"]
    D -->|"Roslyn compilation"| E["Type-safe .NET API<br/>with version guards"]
    E -->|"CommandExecutor + IProcessRunner"| F["Process execution"]
    F -->|"IOutputParser → IResultCollector"| G["Typed results"]

    style A fill:#1a1a2e,stroke:#e94560,color:#eee
    style B fill:#16213e,stroke:#0f3460,color:#eee
    style C fill:#16213e,stroke:#0f3460,color:#eee
    style D fill:#0f3460,stroke:#533483,color:#eee
    style E fill:#0f3460,stroke:#533483,color:#eee
    style F fill:#2d3436,stroke:#636e72,color:#eee
    style G fill:#2d3436,stroke:#636e72,color:#eee
```

---

## Test Coverage

| Project | Tests | Focus |
|---------|-------|-------|
| `BinaryWrapper.Tests` | 84+ | BinaryIdentifier, SemanticVersion, ProcessRunner, CommandExecutor, VersionGuard |
| `BinaryWrapper.SourceGenerator.Tests` | 146+ | CommandTreeReader, NamingHelper, VersionDiffer, all emitters |
| `BinaryWrapper.Design.Tests` | 40+ | CommandNode, CommandTree serialization, help parsing, scraping |

All tests use xUnit with CsCheck for property-based testing of core abstractions.
