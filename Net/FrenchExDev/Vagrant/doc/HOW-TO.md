# How-To Guide

## Using the Vagrant wrapper

### Creating a client

The entry point is the static `Vagrant.Create()` factory, which takes a `BinaryBinding`:

```csharp
var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("vagrant"),
    ExecutablePath = "/usr/bin/vagrant",
    DetectedVersion = SemanticVersion.Parse("2.4.9")
};

var client = Vagrant.Create(binding);
```

You can also resolve the binding dynamically via an `IBinaryResolver` and `IVersionDetector`.

### Building and executing commands

Every command uses a fluent builder pattern. Pass a configuration lambda to the client method:

```csharp
// Simple command
var destroyCmd = client.Destroy(b => b
    .WithForce(true)
);

// Command with options
var upCmd = client.Up(b => b
    .WithProvider("virtualbox")
    .WithNoProvision(true)
    .WithMachineReadable(true)
);

// Nested command group
var boxListCmd = client.Box.List(b => b
    .WithBoxInfo(true)
);

// Deep nesting
var authCmd = client.Cloud.Auth.Login(b => { });
```

### Executing commands

Commands are data objects (`ICliCommand`). Execute them via `CommandExecutor`:

```csharp
var executor = new CommandExecutor(binaryResolver);

// Simple execution -- returns raw ProcessOutput
var output = await executor.ExecuteAsync(
    new BinaryIdentifier("vagrant"), upCmd);

// Parsed execution -- returns structured result
var result = await executor.ExecuteAsync(
    new BinaryIdentifier("vagrant"),
    upCmd,
    new VagrantOutputParser(),
    new VagrantUpCollector()
);

if (result.Success)
    Console.WriteLine($"Machines ready: {string.Join(", ", result.MachinesReady)}");
else
    Console.WriteLine($"Errors: {string.Join("\n", result.Errors)}");

// Streaming execution -- IAsyncEnumerable<VagrantEvent>
await foreach (var evt in executor.StreamAsync(
    new BinaryIdentifier("vagrant"), upCmd, new VagrantOutputParser()))
{
    switch (evt)
    {
        case VagrantMachineOutput o:
            Console.WriteLine($"[{o.MachineName}] {o.Message}");
            break;
        case VagrantMachineError e:
            Console.Error.WriteLine($"[{e.MachineName}] ERROR: {e.Message}");
            break;
    }
}
```

### Choosing a parser

| Parser | Use when |
|--------|----------|
| `VagrantOutputParser` | Running commands without `--machine-readable` |
| `VagrantMachineReadableParser` | Running commands with `--machine-readable` flag |

Both produce `VagrantEvent` instances and can be used with any `IResultCollector<VagrantEvent, T>`.

### Writing a custom result collector

Implement `IResultCollector<VagrantEvent, TResult>` to aggregate events into your own result type:

```csharp
public sealed class VagrantStatusCollector : IResultCollector<VagrantEvent, IReadOnlyList<string>>
{
    private readonly List<string> _machines = [];

    public void OnEvent(VagrantEvent @event)
    {
        if (@event is VagrantMachineOutput { Message: var msg } output
            && msg.Contains("running"))
        {
            _machines.Add(output.MachineName);
        }
    }

    public IReadOnlyList<string> Complete() => _machines.AsReadOnly();
}
```

---

## Scraping new Vagrant versions

### Prerequisites

- **podman** (or docker) installed and running
- Network access to `releases.hashicorp.com`
- The Design project built: `dotnet build src/FrenchExDev.Net.Vagrant.Design`

### Listing available versions

```bash
dotnet run --project src/FrenchExDev.Net.Vagrant.Design -- --list
```

Filter to recent versions:

```bash
dotnet run --project src/FrenchExDev.Net.Vagrant.Design -- --list --min-version 2.4.0
```

### Full scrape pipeline

The scraper runs in two phases:

**Phase 1: Build images** -- Downloads each Vagrant `.deb`, installs it in a `debian:bookworm` container, applies the WSL fix, and commits the container as a reusable image (`vagrant-scrape:{version}`).

**Phase 2: Scrape** -- Starts containers from pre-built images, runs `vagrant <cmd> -h` recursively for every command, parses the output via `VagrantHelpParser`, and writes JSON to `scrape/`.

Run both phases:

```bash
dotnet run --project src/FrenchExDev.Net.Vagrant.Design -- \
    --min-version 2.4.3 \
    --parallel 4
```

Build images only (useful for pre-caching):

```bash
dotnet run --project src/FrenchExDev.Net.Vagrant.Design -- \
    --build-images \
    --min-version 2.4.3
```

### CLI options

| Flag | Default | Description |
|------|---------|-------------|
| `--parallel N` | `4` | Number of concurrent scrape workers |
| `--output DIR` | `Vagrant/src/.../scrape` | Output directory for JSON files |
| `--min-version VER` | none | Only process versions >= VER |
| `--runtime BIN` | `podman` | Container runtime binary |
| `--list` | off | List versions and exit |
| `--build-images` | off | Build images and exit (skip scraping) |

### Using docker instead of podman

```bash
dotnet run --project src/FrenchExDev.Net.Vagrant.Design -- \
    --runtime docker \
    --min-version 2.4.3
```

### JSON output format

Each version produces a file like `vagrant-2.4.9.json`:

```json
{
  "binaryName": "vagrant",
  "root": {
    "name": "vagrant",
    "description": "...",
    "options": [
      {
        "longName": "[no-]color",
        "shortName": null,
        "description": "Enable or disable color output",
        "valueKind": "flag",
        "clrType": "bool",
        "isRequired": false
      }
    ],
    "subCommands": [
      {
        "name": "up",
        "options": [...],
        "subCommands": []
      },
      {
        "name": "box",
        "subCommands": [
          { "name": "add", "options": [...] },
          { "name": "list", "options": [...] }
        ]
      }
    ]
  }
}
```

After scraping, rebuild the main library to regenerate the C# code:

```bash
dotnet build src/FrenchExDev.Net.Vagrant
```

---

## Running tests

### Full test suite

```bash
dotnet test test/FrenchExDev.Net.Vagrant.Tests
```

### Test categories

The test suite includes:

**Unit tests** -- Output parsing, machine-readable parsing, event equality, result collector logic

**Fuzz tests** (CsCheck) -- Property-based tests that feed arbitrary input to parsers and collectors, asserting no exceptions are thrown:

```csharp
[Fact]
public void ArbitraryInput_NeverThrows()
{
    Gen.String.Sample(text => {
        var parser = new VagrantOutputParser();
        var events = parser.ParseLine(new OutputLine(text, OutputSource.StdOut)).ToList();
        events.ShouldNotBeNull();
    });
}
```

**Round-trip tests** -- Verify that constructing known output patterns and parsing them produces the expected events back

**Integration tests** -- Full lifecycle simulation with multi-line output representing `vagrant up` success, failure, and multi-machine scenarios

### Code coverage

```bash
dotnet test test/FrenchExDev.Net.Vagrant.Tests \
    --collect:"XPlat Code Coverage"
```

---

## Extending the wrapper

### Adding a new output parser

Implement `IOutputParser<VagrantEvent>`:

```csharp
public sealed class MyCustomParser : IOutputParser<VagrantEvent>
{
    public IEnumerable<VagrantEvent> ParseLine(OutputLine line)
    {
        // Your parsing logic
        yield return new VagrantOutputLine(line.Text, line.Source);
    }

    public IEnumerable<VagrantEvent> Complete(int exitCode)
    {
        if (exitCode != 0)
            yield return new VagrantMachineError("vagrant", $"Exit code: {exitCode}");
    }
}
```

### Adding new event types

Add new sealed records extending `VagrantEvent` in `VagrantEvents.cs`:

```csharp
public sealed record VagrantBoxDownloaded(string BoxName, string Provider) : VagrantEvent;
```

Then update your parser to emit the new event type.

### Supporting a new Vagrant version

1. Run the scraper with `--min-version` set to the new version
2. The new JSON file appears in `scrape/`
3. Rebuild -- the source generator automatically picks up the new file and adjusts `[SinceVersion]` / `[UntilVersion]` attributes
4. Run tests to verify nothing broke

---

## Troubleshooting

### Scrape failures on specific versions

**Versions 2.4.4--2.4.5** have a known `server_mode?` bug. Sub-commands like `box`, `cloud`, and `plugin` crash when run with `-h`. The scraper handles this -- these versions produce valid but sparser JSON (fewer sub-commands). The version differ fills in the gaps from adjacent versions.

### WSL-related crashes during scraping

If scraping fails with Ruby errors mentioning WSL or platform detection, the WSL patch may not have applied. Verify the image was built correctly:

```bash
podman run --rm vagrant-scrape:2.4.9 \
    grep '_wsl' /opt/vagrant/embedded/gems/gems/vagrant-*/lib/vagrant/util/platform.rb
```

Expected: `@_wsl = false`

### "Command not supported" at runtime

A `CommandNotSupportedException` means the `BinaryBinding.DetectedVersion` is outside the scraped version range. Either:
- Update the binding's `DetectedVersion` to match the actual binary
- Scrape the target version to extend coverage

### Generated code not updating

Ensure scrape JSON files are registered as `AdditionalFiles` in the `.csproj`:

```xml
<AdditionalFiles Include="scrape\vagrant-*.json" />
```

Clean and rebuild:

```bash
dotnet clean src/FrenchExDev.Net.Vagrant
dotnet build src/FrenchExDev.Net.Vagrant
```

Generated files are emitted to `obj/Generated/` when `EmitCompilerGeneratedFiles` is enabled.
