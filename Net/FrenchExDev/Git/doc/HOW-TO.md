# How To

## Scraping Git Versions

### Prerequisites

- Podman (or Docker) running
- Internet access (downloads source tarballs from GitHub)

### Scrape All Missing Versions

```bash
cd Net/FrenchExDev/Git/src/FrenchExDev.Net.Git.Design
dotnet run -- --missing --parallel 2
```

### Scrape a Single Version

```bash
dotnet run -- --min-version 2.45.0 --parallel 1
```

### List Available Versions

```bash
dotnet run -- --list
dotnet run -- --list --missing   # only versions not yet scraped
```

### Reparse Cached Help Text

If the parser is updated and you want to regenerate JSON without re-running containers:

```bash
dotnet run -- --reparse
```

### Dashboard Mode

```bash
dotnet run -- --missing --parallel 4 --dashboard
```

## Using the Generated API

After scraping, the main project generates typed commands. Usage:

```csharp
using FrenchExDev.Net.Git;

// Create a client
var client = Git.Create(binding);

// Build and inspect a command
var cmd = await client.AddAsync(b => b.WithAll(true));
var args = cmd.ToArguments(); // ["--all"]

// Nested sub-groups
var remote = client.Remote;
var addCmd = await remote.AddAsync(b => b.WithName("origin").WithUrl("https://..."));
```

## Running Tests

```bash
cd Net/FrenchExDev/Git
dotnet test
```

## Quality Gate

```bash
cd Net/FrenchExDev
dotnet run --project QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test --config Git/quality-gate.yml
```
