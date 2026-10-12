# VirtualBox.Version — Claude Context

Lightweight .NET library for discovering the locally installed VirtualBox version and retrieving official SHA256 checksums from the VirtualBox download repository.

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [WRAPPER-VERSIONING](../../../Skills/Net/Programming/WRAPPER-VERSIONING/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.VirtualBox.Version.slnx`

## Notes for Claude
- This is **not** a BinaryWrapper consumer. It is a focused version-discovery library: detect the installed VBox via `VBoxManage --version` and look up checksums from the official download server.
- Two 1-method-style interfaces: `IVirtualBoxSystemVersionDiscoverer` and `IVirtualBoxVersionInformationSearcher`. ISP-compliant — keep them narrow.
- Pure .NET 10.0 with zero NuGet dependencies. Do not add any.
- All operations accept `CancellationToken` and return `Task<...>`. Async-first.
- `HttpClient` on the searcher is injectable for testing — the default is internal.
- Records (`VirtualBoxVersionRecord`, `VirtualBoxVersionInfos`, `VirtualBoxVersionInformationSearchingFilters`) are immutable. Do not convert to mutable classes.
