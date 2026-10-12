# FrenchExDev.Net.Alpine.Version

A .NET 10.0 library for discovering, parsing, and comparing Alpine Linux versions. It queries the official Alpine Linux CDN to search for available releases with support for filtering by version range, architecture, flavor, and release candidate status.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [API Reference](#api-reference)
  - [AlpineVersion](#alpineversion)
  - [AlpineVersionSearcher](#alpineversionsearcher)
  - [AlpineVersionSearchingFiltersBuilder](#alpineversionsearchingfiltersbuilder)
  - [AlpineVersionSearchingFilters](#alpineversionsearchingfilters)
  - [Enumerations](#enumerations)
  - [Records](#records)
  - [Extension Methods](#extension-methods)
- [Usage Examples](#usage-examples)
  - [Parsing Versions](#parsing-versions)
  - [Comparing Versions](#comparing-versions)
  - [Searching for Releases](#searching-for-releases)
  - [Filtering by Architecture and Flavor](#filtering-by-architecture-and-flavor)
  - [Working with Checksums](#working-with-checksums)
- [Architecture](#architecture)
- [Testing](#testing)
- [Project Structure](#project-structure)
- [Dependencies](#dependencies)

---

## Features

- **Version parsing** with support for semantic versions, `edge`, and release candidates (e.g. `3.19.0_rc1`)
- **Version comparison** via `IComparable<AlpineVersion>` and operator-based comparisons
- **CDN search** that queries `dl-cdn.alpinelinux.org` for available releases
- **Fluent builder API** to configure search filters with method chaining
- **Parallel HTTP requests** (up to 10 concurrent) for fast multi-version discovery
- **SHA-256 and SHA-512 checksums** fetched alongside each release record
- **Async-first** design with full `CancellationToken` support

---

## Installation

Add a project reference (this package is not yet published to NuGet):

```xml
<ProjectReference Include="..\Alpine.Version\src\FrenchExDev.Net.Alpine.Version\FrenchExDev.Net.Alpine.Version.csproj" />
```

---

## Quick Start

```csharp
using FrenchExDev.Net.Alpine.Version;
using FrenchExDev.Net.HttpClient;

// Create a searcher with an HTTP client
var httpClient = new StandardHttpClient();
var searcher = new AlpineVersionSearcher(httpClient);

// Search for Alpine 3.20 Standard x86_64 releases
var results = await searcher.SearchAsync(f => f
    .WithExactVersion("3.20.0")
    .WithFlavor(AlpineFlavors.Standard)
    .WithArch(AlpineArchitectures.x86_64));

foreach (var release in results)
{
    Console.WriteLine($"{release.Version} {release.Architecture} {release.Flavor}");
    Console.WriteLine($"  URL:    {release.Url}");
    Console.WriteLine($"  SHA256: {release.Sha256}");
}
```

---

## API Reference

### AlpineVersion

Represents an Alpine Linux version with parsing and comparison capabilities.

#### Properties

| Property | Type | Description |
|---|---|---|
| `Major` | `string` | Major version component (can be `"edge"`) |
| `Minor` | `string` | Minor version component |
| `Patch` | `string` | Patch version component (may include RC suffix) |
| `IsEdge` | `bool` | `true` if this is the edge (rolling) release |
| `MajorNumber` | `int` | Numeric major version (`0` if non-numeric) |
| `MinorNumber` | `int` | Numeric minor version (`0` if non-numeric) |
| `PatchNumber` | `int` | Numeric patch version (`0` if non-numeric) |
| `Rc` | `string` | RC string (e.g. `"rc1"`), empty if not an RC |
| `RcNumber` | `int` | Numeric RC number (`0` if not an RC) |
| `IsMajorNumber` | `bool` | `true` if Major is a valid integer |
| `IsMinorNumber` | `bool` | `true` if Minor is a valid integer |
| `IsPatchNumber` | `bool` | `true` if Patch (before `_`) is a valid integer |
| `IsRcNumber` | `bool` | `true` if Patch contains an RC suffix |

#### Factory Method

```csharp
// Parse a version string
AlpineVersion version = AlpineVersion.From("3.18.2");
AlpineVersion rc      = AlpineVersion.From("3.19.0_rc1");
AlpineVersion edge    = AlpineVersion.From("edge");
```

#### Comparison

```csharp
// IComparable
int result = version1.CompareTo(version2);

// Operator-based
bool isNewer = AlpineVersion.Compare(v1, AlpineVersion.Operator.GreaterThan, v2);
```

**Comparison precedence** (via `MajorMinorPatchRelationalComparer`):
1. Edge status (non-edge sorts before edge)
2. Major version (numeric)
3. Minor version (numeric)
4. Patch version (numeric)
5. RC number (numeric)

#### String Formatting

```csharp
version.ToString();        // "3.18.2"
version.ToMajorMinor();    // "3.18"
version.ToMajorMinorUrl(); // "v3.18" (or "edge" for edge versions)
```

---

### AlpineVersionSearcher

Service that queries the Alpine Linux CDN to discover available releases.

Implements `IAlpineVersionSearcher`.

#### Constructor

```csharp
var searcher = new AlpineVersionSearcher(IHttpClient httpClient);
```

#### Methods

```csharp
// Fluent API
Task<AlpineVersionList> SearchAsync(
    Action<AlpineVersionSearchingFiltersBuilder> configureSearchFilter,
    CancellationToken cancellationToken = default);

// Direct filters
Task<AlpineVersionList> SearchAsync(
    AlpineVersionSearchingFilters filters,
    CancellationToken cancellationToken = default);
```

The searcher follows this sequence:
1. Query the CDN root (`/alpine/`) for available version directories
2. Filter versions by min/max/exact constraints
3. For each matching version, query architecture directories in parallel
4. For each version-architecture pair, parse ISO filenames to extract flavors
5. Apply architecture, flavor, and RC filters
6. Fetch SHA-256 and SHA-512 checksums in parallel
7. Return results sorted by version (ascending)

---

### AlpineVersionSearchingFiltersBuilder

Fluent builder for configuring search filters. All methods return the builder for chaining.

| Method | Description |
|---|---|
| `WithMinimumVersion(string)` | Set the minimum version (inclusive) |
| `WithMaximumVersion(string)` | Set the maximum version (inclusive) |
| `WithExactVersion(string)` | Search for a specific version only |
| `WithFlavors(List<AlpineFlavors>)` | Filter by multiple flavors |
| `WithFlavor(AlpineFlavors)` | Filter by a single flavor |
| `WithArchs(List<AlpineArchitectures>)` | Filter by multiple architectures |
| `WithArch(AlpineArchitectures)` | Filter by a single architecture |
| `WithRc(bool)` | Include or exclude release candidates (default: `false`) |
| `Build()` | Build the immutable `AlpineVersionSearchingFilters` |

---

### AlpineVersionSearchingFilters

Immutable configuration object produced by the builder.

| Property | Type | Description |
|---|---|---|
| `MinimumVersion` | `AlpineVersion?` | Lower bound (inclusive) |
| `MaximumVersion` | `AlpineVersion?` | Upper bound (inclusive) |
| `ExactVersion` | `AlpineVersion?` | Exact version match |
| `Flavors` | `List<AlpineFlavors>?` | Allowed flavors (`null` = all) |
| `Architectures` | `List<AlpineArchitectures>?` | Allowed architectures (`null` = all) |
| `Rc` | `bool` | Whether to include release candidates |

---

### Enumerations

#### AlpineArchitectures

CPU architectures for Alpine Linux distributions.

| Value | Description |
|---|---|
| `aarch64` | 64-bit ARM |
| `armhf` | ARM hard-float |
| `armv7` | ARMv7 |
| `cloud` | Cloud-optimized |
| `ppc64le` | PowerPC 64-bit LE |
| `s390x` | IBM System/390 |
| `x86` | 32-bit x86 |
| `x86_64` | 64-bit x86 |

#### AlpineFlavors

Distribution flavor variants.

| Value | Description |
|---|---|
| `Extended` | Extended variant with extra packages |
| `MiniRootFs` | Minimal root filesystem (containers) |
| `NetBoot` | Network boot image |
| `Standard` | Standard installation ISO |
| `Virt` | Optimized for virtual machines |
| `Xen` | Xen hypervisor variant |

#### AlpineVersion.Operator

Comparison operators for `AlpineVersion.Compare()`.

`Equal`, `NotEqual`, `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`

---

### Records

#### AlpineVersionArchFlavorRecord

Complete release metadata returned by the searcher.

| Property | Type | Description |
|---|---|---|
| `Version` | `string` | Full version string |
| `Architecture` | `string` | Architecture name |
| `Flavor` | `string` | Flavor name |
| `Url` | `string` | Download URL |
| `Sha256` | `string` | SHA-256 checksum |
| `Sha512` | `string` | SHA-512 checksum |

Also exposes `VersionContainsDots()` to check if the version uses dotted notation.

#### AlpineVersionArchRecord

A version-architecture pair (internal intermediate result).

| Property | Type |
|---|---|
| `Version` | `string` |
| `Architecture` | `string` |

#### AlpineVersionRecord

A simple version wrapper.

| Property | Type |
|---|---|
| `Version` | `string` |

#### AlpineVersionList

`class AlpineVersionList : List<AlpineVersionArchFlavorRecord>` - strongly-typed list of release records.

---

### Extension Methods

```csharp
// Convert a string directly to AlpineVersion
AlpineVersion v = "3.18.2".AsAlpineVersion();
```

Provided by the static class `AlpineVersionBuilder`.

---

## Usage Examples

### Parsing Versions

```csharp
var stable = AlpineVersion.From("3.20.3");
// Major="3", Minor="20", Patch="3"

var rc = AlpineVersion.From("3.21.0_rc1");
// Major="3", Minor="21", Patch="0_rc1", Rc="rc1", RcNumber=1

var edge = AlpineVersion.From("edge");
// Major="edge", IsEdge=true

// Extension method shorthand
var v = "3.18.0".AsAlpineVersion();
```

### Comparing Versions

```csharp
var v1 = AlpineVersion.From("3.18.0");
var v2 = AlpineVersion.From("3.20.0");

// IComparable
v1.CompareTo(v2); // -1 (v1 < v2)

// Operator-based
AlpineVersion.Compare(v1, AlpineVersion.Operator.LessThan, v2);    // true
AlpineVersion.Compare(v1, AlpineVersion.Operator.Equal, v2);       // false

// Edge always sorts after stable
var edge = AlpineVersion.From("edge");
AlpineVersion.Compare(v2, AlpineVersion.Operator.LessThan, edge);  // true
```

### Searching for Releases

```csharp
var httpClient = new StandardHttpClient();
var searcher = new AlpineVersionSearcher(httpClient);

// Search with a version range
var results = await searcher.SearchAsync(f => f
    .WithMinimumVersion("3.18.0")
    .WithMaximumVersion("3.20.0"));

// Search for an exact version
var exact = await searcher.SearchAsync(f => f
    .WithExactVersion("3.20.3"));

// Include release candidates
var withRc = await searcher.SearchAsync(f => f
    .WithMinimumVersion("3.21.0")
    .WithRc());
```

### Filtering by Architecture and Flavor

```csharp
// Single architecture and flavor
var results = await searcher.SearchAsync(f => f
    .WithExactVersion("3.20.0")
    .WithArch(AlpineArchitectures.x86_64)
    .WithFlavor(AlpineFlavors.Virt));

// Multiple architectures
var multiArch = await searcher.SearchAsync(f => f
    .WithExactVersion("3.20.0")
    .WithArchs([AlpineArchitectures.x86_64, AlpineArchitectures.aarch64])
    .WithFlavors([AlpineFlavors.Standard, AlpineFlavors.Virt]));
```

### Working with Checksums

```csharp
var results = await searcher.SearchAsync(f => f
    .WithExactVersion("3.20.3")
    .WithArch(AlpineArchitectures.x86_64)
    .WithFlavor(AlpineFlavors.Standard));

foreach (var release in results)
{
    Console.WriteLine($"Version:  {release.Version}");
    Console.WriteLine($"Arch:     {release.Architecture}");
    Console.WriteLine($"Flavor:   {release.Flavor}");
    Console.WriteLine($"URL:      {release.Url}");
    Console.WriteLine($"SHA-256:  {release.Sha256}");
    Console.WriteLine($"SHA-512:  {release.Sha512}");
}
```

### Using with CancellationToken

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var results = await searcher.SearchAsync(
    f => f.WithMinimumVersion("3.18.0").WithArch(AlpineArchitectures.x86_64),
    cts.Token);
```

### Using the Direct Filters API

```csharp
var filters = new AlpineVersionSearchingFilters
{
    MinimumVersion = AlpineVersion.From("3.18.0"),
    MaximumVersion = AlpineVersion.From("3.20.0"),
    Architectures = [AlpineArchitectures.x86_64],
    Flavors = [AlpineFlavors.Standard],
    Rc = false
};

var results = await searcher.SearchAsync(filters);
```

---

## Architecture

See [doc/ARCHITECTURE.md](doc/ARCHITECTURE.md) for detailed architecture documentation including:
- Component diagrams (Mermaid)
- Sequence diagrams for search and comparison workflows
- Design decisions and rationale
- Best practices

### High-Level Design

```
AlpineVersionSearcher
    |
    +--> IHttpClient (injected)
    |        |
    |        +--> Alpine CDN (dl-cdn.alpinelinux.org)
    |
    +--> AlpineVersionSearchingFilters (configuration)
    |        |
    |        +--> AlpineVersionSearchingFiltersBuilder (fluent API)
    |
    +--> AlpineVersionList (results)
             |
             +--> AlpineVersionArchFlavorRecord (version + arch + flavor + checksums)
                      |
                      +--> AlpineVersion (parsing + comparison)
```

### Key Design Decisions

| Decision | Rationale |
|---|---|
| String-based version components | Supports `edge` and RC suffixes naturally; numeric accessors provided when needed |
| `Parallel.ForEachAsync` with max 10 | Balances performance (10x vs sequential) with responsible CDN usage |
| Fluent builder for filters | Makes optional parameters explicit; enables readable method chaining |
| `ConcurrentBag<T>` for aggregation | Thread-safe collection for parallel HTTP result merging |
| `IHttpClient` abstraction | Enables unit testing with fakes without hitting the real CDN |

---

## Testing

### Running Tests

```bash
dotnet test Alpine.Version/FrenchExDev.Net.Alpine.Version.slnx
```

### Running with Quality Gate

```bash
dotnet run --project ../QualityGate/src/FrenchExDev.Net.QualityGate.Cli -- test \
    --quality-gate Alpine.Version/quality-gate.yml
```

### Test Structure

| Test Class | Type | Description |
|---|---|---|
| `AlpineVersionComparerTests` | Unit | 102 parametrized comparison tests across operators and version ranges |
| `AlpineVersionSearcherTests` | Unit + Integration | Searcher tests with fake HTTP responses and live CDN integration tests |

The searcher tests use `FakeHttpClientBuilder` from `FrenchExDev.Net.HttpClient.Testing` to mock CDN responses, enabling offline unit testing of the full search pipeline.

Integration tests (marked with `[Trait("integration", "internet")]`) hit the real Alpine CDN and verify end-to-end correctness.

### Test Helper

`AlpineVersionSearcherTester` provides reusable validation methods:

```csharp
await AlpineVersionSearcherTester.ValidAsync(
    httpBuilder => httpBuilder.WithFakeGet(...),
    filters => filters.WithExactVersion("3.18.0"),
    results => results.Count.ShouldBe(1));
```

---

## Project Structure

```
Alpine.Version/
|-- src/
|   +-- FrenchExDev.Net.Alpine.Version/
|       |-- Code.cs                              # All library types
|       +-- FrenchExDev.Net.Alpine.Version.csproj
|-- test/
|   +-- FrenchExDev.Net.Alpine.Version.Tests/
|       |-- AlpineVersionComparerTests.cs        # Version comparison tests
|       |-- AlpineVersionSearcherTests.cs        # Searcher unit + integration tests
|       |-- Tester.cs                            # Test helper utilities
|       +-- FrenchExDev.Net.Alpine.Version.Tests.csproj
|-- doc/
|   +-- ARCHITECTURE.md                          # Detailed architecture documentation
|-- FrenchExDev.Net.Alpine.Version.slnx          # Solution file
|-- quality-gate.yml                             # Quality gate configuration
|-- coverage.runsettings                         # Code coverage settings
+-- README.md                                    # This file
```

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `FrenchExDev.Net.Builder` | Builder pattern base classes |
| `FrenchExDev.Net.HttpClient` | HTTP client abstraction (`IHttpClient`) |
| `FrenchExDev.Net.HttpClient.Testing` | Fake HTTP client for testing |

**Target Framework:** .NET 10.0

**Package Version:** 0.0.9
