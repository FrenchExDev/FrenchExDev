# VirtualBox.Version

Lightweight .NET library for discovering the locally installed VirtualBox version and retrieving official SHA256 checksums from the VirtualBox download repository.

## Quick Start

```csharp
// Discover installed VirtualBox version
IVirtualBoxSystemVersionDiscoverer discoverer = new VirtualBoxSystemVersionDiscoverer();
VirtualBoxVersionRecord version = await discoverer.DiscoverAsync();
Console.WriteLine(version.ToStringWithoutRelease()); // "7.0.14"

// Search for Guest Additions ISO checksum
IVirtualBoxVersionInformationSearcher searcher = new VirtualBoxVersionInformationSearcher();
var results = await searcher.SearchAsync(
    new VirtualBoxVersionInformationSearchingFilters("7.0.14"));
Console.WriteLine(results[0].AdditionsIsoSha256); // SHA256 hash
```

## Projects

| Project | TFM | Purpose |
|---------|-----|---------|
| `VirtualBox.Version` | net10.0 | Version discovery + checksum retrieval |

## Key Design Decisions

- **Zero external dependencies** -- pure .NET 10.0, no NuGet packages
- **Interface-driven** -- `IVirtualBoxSystemVersionDiscoverer` and `IVirtualBoxVersionInformationSearcher` for DI and testability
- **Immutable records** -- `VirtualBoxVersionRecord`, `VirtualBoxVersionInfos`, `VirtualBoxVersionInformationSearchingFilters`
- **Async-first** -- all operations support `CancellationToken`
- **HttpClient injectable** -- `VirtualBoxVersionInformationSearcher` accepts an optional `HttpClient` for reuse and testing

## Documentation

- [ARCHITECTURE.md](doc/ARCHITECTURE.md) -- interfaces, implementations, data models
- [HOW-TO.md](doc/HOW-TO.md) -- usage examples, DI registration, testing
- [PHILOSOPHY.md](doc/PHILOSOPHY.md) -- why version discovery is a separate concern

## Building

```bash
dotnet build VirtualBox.Version/FrenchExDev.Net.VirtualBox.Version.slnx
```
