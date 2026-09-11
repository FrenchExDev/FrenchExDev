# VirtualBox.Version -- Developer Guide (HOW-TO)

## Table of Contents

1. [Discovering the Installed Version](#1-discovering-the-installed-version)
2. [Retrieving Checksums](#2-retrieving-checksums)
3. [DI Registration](#3-di-registration)
4. [HttpClient Reuse](#4-httpclient-reuse)
5. [Error Handling](#5-error-handling)
6. [Testing](#6-testing)

---

## 1. Discovering the Installed Version

```csharp
var discoverer = new VirtualBoxSystemVersionDiscoverer();
VirtualBoxVersionRecord version = await discoverer.DiscoverAsync();

Console.WriteLine($"VirtualBox {version.Major}.{version.Minor}.{version.Patch}");
Console.WriteLine($"Release: {version.ReleaseNumber}");
Console.WriteLine($"Short: {version.ToStringWithoutRelease()}"); // "7.0.14"
```

**Prerequisite:** `VBoxManage` must be in the system PATH. On Windows, VirtualBox adds it during installation. On Linux, ensure the `virtualbox` package is installed.

---

## 2. Retrieving Checksums

```csharp
var searcher = new VirtualBoxVersionInformationSearcher();
var results = await searcher.SearchAsync(
    new VirtualBoxVersionInformationSearchingFilters("7.0.14"));

foreach (var info in results)
{
    Console.WriteLine($"Version: {info.Version}");
    Console.WriteLine($"Guest Additions SHA256: {info.AdditionsIsoSha256}");
}
```

The searcher downloads the official `SHA256SUMS` file from `download.virtualbox.org` and extracts the hash for the VBoxGuestAdditions ISO.

---

## 3. DI Registration

```csharp
services.AddSingleton<IVirtualBoxSystemVersionDiscoverer, VirtualBoxSystemVersionDiscoverer>();
services.AddSingleton<IVirtualBoxVersionInformationSearcher, VirtualBoxVersionInformationSearcher>();
```

Both interfaces are designed for constructor injection.

---

## 4. HttpClient Reuse

`VirtualBoxVersionInformationSearcher` accepts an optional `HttpClient` to avoid socket exhaustion in long-running applications:

```csharp
var httpClient = new HttpClient();
var searcher = new VirtualBoxVersionInformationSearcher(httpClient);
```

Or via `IHttpClientFactory`:

```csharp
services.AddHttpClient<VirtualBoxVersionInformationSearcher>();
```

---

## 5. Error Handling

| Scenario | Exception |
|----------|-----------|
| VBoxManage not in PATH | `Exception` |
| ExactVersion filter is null/empty | `InvalidDataException` |
| Network failure during SHA256SUMS download | `HttpRequestException` |

---

## 6. Testing

The `test/` directory is currently empty. Recommended test strategy:

```csharp
// Version parsing (mock the process output)
// Checksum retrieval (inject HttpClient with MockHandler)

var handler = new MockHttpMessageHandler(
    "abc123def456 *VBoxGuestAdditions_7.0.14.iso\n");
var client = new HttpClient(handler);
var searcher = new VirtualBoxVersionInformationSearcher(client);

var results = await searcher.SearchAsync(
    new VirtualBoxVersionInformationSearchingFilters("7.0.14"));
results[0].AdditionsIsoSha256.ShouldBe("abc123def456");
```
