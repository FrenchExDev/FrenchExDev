# LOCAL-NUGET-REGISTRY — Requirements

## Mandatory

- The registry directory exists at:
  `C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__`
- The directory is registered as a NuGet source named `FrenchExDevLocal` (per-user or
  per-repo `nuget.config`).
- Every locally-published `.nupkg` is named `<PackageId>.<Version>.nupkg` and lives at
  the registry root (no subdirectories).
- Any package consumed from the local registry has its version declared in
  [Directory.Packages.props](../../../../Net/FrenchExDev/Directory.Packages.props) —
  there is no exception to CPM.
- Source generator packages place their analyzer assemblies in
  `analyzers/dotnet/cs/` inside the `.nupkg`.
- Versions are bumped before re-publishing — do not overwrite existing `.nupkg` files
  in place.

## Forbidden

- Hard-coding `<RestoreSources>` in `.csproj` files. Use `nuget.config` or per-user
  registration only.
- Publishing to the local registry without bumping the version (causes silent stale
  consumption).
- Storing multiple `.nupkg` files of the same id+version (NuGet's behaviour is
  undefined).
- Putting non-`.nupkg` files in the registry directory.
- Relying on the local registry for CI builds — CI must consume from a private feed
  (Azure Artifacts, GitHub Packages, etc.) or pack-and-test in a single job.

## Verification

```bash
# 1. Source registered
dotnet nuget list source | grep FrenchExDevLocal

# 2. Directory exists and contains .nupkg files
ls "C:/code/FrenchExDev.Net/FrenchExDev.Net_i2/FrenchExDev.Net/__Local_Nuget_Registry__/"*.nupkg

# 3. Restore picks up the local package
dotnet restore Net/FrenchExDev/Consumer/FrenchExDev.Net.Consumer.slnx --verbosity normal \
  | grep "FrenchExDev.Net.MyPackage"
```
