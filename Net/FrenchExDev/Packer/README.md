# FrenchExDev.Net.Packer

Typed C# wrapper for [HashiCorp Packer](https://www.packer.io/) built on the BinaryWrapper framework.

## Quick Start

```csharp
var client = Packer.Create(new BinaryBinding
{
    Identifier = new BinaryIdentifier("packer"),
    ExecutablePath = "/usr/local/bin/packer"
});

// Build with typed fluent API
var buildCmd = client.Build(b => b
    .WithTemplate("aws.pkr.hcl")
    .WithForce(true)
    .WithVar(["region=us-east-1"]));

// Validate
var validateCmd = client.Validate(b => b
    .WithTemplate("aws.pkr.hcl")
    .WithSyntaxOnly(true));

// Nested plugin commands
var installCmd = client.Plugins.Install(b => b
    .WithPlugin("github.com/hashicorp/docker")
    .WithForce(true));
```

## Commands

| Command | Method | Options |
|---|---|---|
| `build` | `client.Build(...)` | force, color, debug, except, only, on-error, parallel-builds, timestamp-ui, var, var-file, machine-readable |
| `validate` | `client.Validate(...)` | syntax-only, except, only, var, var-file, no-warn-undeclared-var |
| `init` | `client.Init(...)` | upgrade, force |
| `fmt` | `client.Fmt(...)` | check, diff, recursive, write |
| `inspect` | `client.Inspect(...)` | (none) |
| `hcl2-upgrade` | `client.Hcl2Upgrade(...)` | output-file, with-annotations |
| `console` | `client.Console(...)` | var, var-file |
| `plugins install` | `client.Plugins.Install(...)` | force |
| `plugins remove` | `client.Plugins.Remove(...)` | (none) |
| `plugins installed` | `client.Plugins.Installed(...)` | (none) |
| `plugins required` | `client.Plugins.Required(...)` | (none) |

## Output Parsing

```csharp
// Parse standard build output
var parser = new PackerBuildParser();

// Parse machine-readable format (-machine-readable flag)
var mrParser = new PackerMachineReadableParser();

// Collect build results
var collector = new PackerBuildCollector();
```

## Architecture

See [doc/ARCHITECTURE.md](doc/ARCHITECTURE.md).
