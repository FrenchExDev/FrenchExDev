# VirtualBox.Version -- Architecture

## 1. Overview

VirtualBox.Version provides two capabilities: discovering the locally installed VirtualBox version (via `VBoxManage --version`) and retrieving official SHA256 checksums from the VirtualBox download CDN. Both are exposed as injectable interfaces with async support.

---

## 2. Project Structure

```
VirtualBox.Version/
  FrenchExDev.Net.VirtualBox.Version.slnx
  src/
    FrenchExDev.Net.VirtualBox.Version/
      Code.cs                    (all types in a single file)
      FrenchExDev.Net.VirtualBox.Version.csproj
  doc/
  test/                          (empty -- awaiting implementation)
```

Single project, zero NuGet dependencies, .NET 10.0.

---

## 3. Interfaces

### IVirtualBoxSystemVersionDiscoverer

```csharp
Task<VirtualBoxVersionRecord> DiscoverAsync(CancellationToken cancellationToken = default);
```

Discovers the installed VirtualBox version by executing `VBoxManage --version` as a child process. Parses the output (e.g. `7.0.14r162485`) into a structured `VirtualBoxVersionRecord`.

### IVirtualBoxVersionInformationSearcher

```csharp
Task<List<VirtualBoxVersionInfos>> SearchAsync(
    VirtualBoxVersionInformationSearchingFilters filters,
    CancellationToken cancellationToken = default);
```

Downloads the `SHA256SUMS` file from `https://download.virtualbox.org/virtualbox/{version}/SHA256SUMS`, parses it, and returns the SHA256 hash for the VBoxGuestAdditions ISO.

---

## 4. Implementations

### VirtualBoxSystemVersionDiscoverer

- Launches `VBoxManage --version` as a `Process` (NoWindow, RedirectStandardOutput)
- Parses output by splitting on `.` and `r`:
  - `7.0.14r162485` -> Major=7, Minor=0, Patch=14, ReleaseNumber=162485
- Throws `Exception` if VBoxManage is not found in PATH

### VirtualBoxVersionInformationSearcher

- Thread-safe (`sealed`)
- Accepts optional `HttpClient` via constructor (or creates internal instance)
- Downloads SHA256SUMS from the official CDN
- Parses checksum format: `<sha256> *<filename>`
- Filters for `VBoxGuestAdditions` ISO files
- Throws `InvalidDataException` if `ExactVersion` filter is null/empty

---

## 5. Data Models

| Record | Properties | Purpose |
|--------|-----------|---------|
| `VirtualBoxVersionRecord` | Major, Minor, Patch, ReleaseNumber | Structured version from VBoxManage output |
| `VirtualBoxVersionInfos` | Version, AdditionsIsoSha256 | Version + Guest Additions checksum |
| `VirtualBoxVersionInformationSearchingFilters` | ExactVersion | Search filter specification |

`VirtualBoxVersionRecord` provides `ToStringWithoutRelease()` -> `"Major.Minor.Patch"`.

---

## 6. Dependency Graph

```
VirtualBox.Version
  -> (none -- zero external dependencies)
  -> System.Diagnostics (Process)
  -> System.Net.Http (HttpClient)
```

Potential consumers in the ecosystem:
- **Vagrant** -- validate VirtualBox version before provisioning
- **Vos** -- verify hypervisor compatibility
- **Packer.Alpine** -- check VirtualBox availability for image builds
