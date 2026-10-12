# How to Quality-Assure a .NET Solution with QualityGate

Step-by-step guide for onboarding any .NET solution into the QualityGate workflow.

## Prerequisites

- .NET 10 SDK installed
- The QualityGate local tool installed

```bash
cd path/to/QualityGate

# First time (or after cloning)
dotnet tool restore

# After modifying CLI source code, re-pack and update
dotnet pack src/FrenchExDev.Net.QualityGate.Cli -o /tmp/quality-gate-tool
dotnet tool update FrenchExDev.Net.QualityGate.Cli --add-source /tmp/quality-gate-tool
```

## 1. Initialize

Scaffold `quality-gate.yml` and `coverage.runsettings` for your solution:

```bash
cd path/to/your/solution

# Auto-detect .slnx/.sln in current directory
dotnet quality-gate init

# Or specify the solution explicitly
dotnet quality-gate init --solution MySolution.slnx

# Overwrite existing config files
dotnet quality-gate init --force
```

This creates:
- **`quality-gate.yml`** — analysis config with default thresholds
- **`coverage.runsettings`** — XPlat Code Coverage config targeting your non-test assemblies

## 2. Validate Setup

Check that config is valid before running:

```bash
dotnet quality-gate validate
```

This verifies:
- Config file parses correctly
- Solution file exists
- Thresholds are in valid ranges
- `coverage.runsettings` is present

## 3. First Run

Run tests with coverage collection, then analyze quality:

```bash
dotnet quality-gate test
```

This does:
1. Cleans old `coverage-results/`
2. Runs `dotnet test --collect:"XPlat Code Coverage"`
3. Loads the solution via Roslyn MSBuildWorkspace
4. Computes all metrics (complexity, coupling, cohesion, etc.)
5. Parses coverage reports
6. Evaluates quality gates
7. Writes JSON report + summary to `.quality-gate/`

### View results in browser

```bash
dotnet quality-gate test --serve
```

Opens an interactive dashboard at `http://localhost:3000`.

## 4. Development Loop

### Interactive mode (recommended for development)

```bash
dotnet quality-gate test --interactive
```

This is shorthand for `--loop --manual --serve`. After each run:
- Results are served at `http://localhost:3000`
- The SPA auto-reloads via WebSocket when new results land
- CLI pauses and waits for you to press **Enter** to re-run
- Press **Ctrl+C** to exit

### Watch mode (auto-trigger on file save)

```bash
dotnet quality-gate test --loop --watch --serve
```

Watches `*.cs`, `*.csproj`, `*.props` files. On save:
- Auto-reruns build + test + analyze
- SPA refreshes automatically

### Combined (watch + manual)

```bash
dotnet quality-gate test --loop --watch --manual --serve
```

Re-runs on either file change OR Enter key press.

## 5. Analysis Only (skip tests)

If coverage data already exists:

```bash
dotnet quality-gate analyze              # one-shot
dotnet quality-gate analyze --serve      # one-shot + serve
dotnet quality-gate analyze --interactive  # loop + pause + serve
```

## 6. CI/CD Integration

```bash
dotnet quality-gate check
```

- Runs full analysis
- Exits with code **1** if any quality gate fails
- No serve, no loop — designed for CI pipelines

Skip build if your CI already built:

```bash
dotnet quality-gate check --no-build
```

## 7. Other Commands

```bash
# Run tests with coverage only (no analysis)
dotnet quality-gate coverage

# Serve previously generated reports
dotnet quality-gate serve
dotnet quality-gate serve --port 8080

# Print interface-to-implementation map
dotnet quality-gate interfaces
```

## Customization

### Adjust Thresholds

Edit `quality-gate.yml`:

```yaml
gates:
  max-cyclomatic-complexity: 20    # raise if you have complex algorithms
  max-class-coupling: 50           # raise for Roslyn/reflection-heavy code
  min-test-quality-score: 0.80     # lower initially, ratchet up over time
```

### Include/Exclude Assemblies from Coverage

Edit `coverage.runsettings`:

```xml
<Include>[MyApp.Core]*,[MyApp.Services]*</Include>
```

Patterns: `[AssemblyName]*` — matches all types in that assembly.

### Exclude Files from Analysis

In `quality-gate.yml`:

```yaml
exclude:
  - "**/obj/**"
  - "**/bin/**"
  - "**/*.Designer.cs"
  - "**/Migrations/**"
```

## Workflow Summary

| Stage | Command | When |
|---|---|---|
| Setup | `init` | Once per solution |
| Validate | `validate` | After editing config |
| Development | `test --interactive` | During active development |
| Watch | `test --loop --watch --serve` | Hands-free continuous feedback |
| CI/CD | `check` | On every PR/push |
| Quick analysis | `analyze` | When coverage already exists |
| Browse reports | `serve` | Anytime |

## Ratcheting Strategy

Start with relaxed thresholds, then tighten as code improves:

1. Run `init` to get defaults
2. Run `test` — see what fails
3. Adjust thresholds in `quality-gate.yml` to match current state
4. Commit the config
5. After each improvement, tighten the threshold
6. Eventually reach strict targets (100% coverage, low complexity, etc.)
