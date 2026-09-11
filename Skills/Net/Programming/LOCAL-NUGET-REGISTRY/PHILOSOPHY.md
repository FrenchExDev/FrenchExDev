# LOCAL-NUGET-REGISTRY — Philosophy

The monorepo publishes some packages to a **file-system NuGet feed** instead of
nuget.org. The feed is a flat folder of `.nupkg` files configured as a NuGet source. It
exists to support four scenarios:

1. **Rapid cross-package iteration.** Bump a package's version, `dotnet pack`, drop the
   `.nupkg` in the local feed, and consumers pick it up on the next `dotnet restore` —
   without going through nuget.org's publish/index/cache pipeline.
2. **Air-gapped builds.** Once packages are in the local feed, no network is needed.
3. **Pre-publication validation.** Try the packed artifact end-to-end before pushing to
   nuget.org.
4. **Source generator iteration.** SG `.nupkg`s can be tested as analyzer
   `<PackageReference>`s instead of as project references, which exposes the same
   loading behaviour the public package will have.

## When to use the local registry

- **Use it** when iterating on a multi-package change, when validating an SG package
  shape, or when nuget.org publication is intentional but not yet ready.
- **Do not use it** for normal in-repo dependencies — those are project references,
  not package references. Reach for the local feed only when the consumer cannot use
  a project reference (e.g. it lives in a different repo, or it must consume the
  generator as an analyzer package).

## Why a path, not a URL

NuGet supports both `https://` URLs and local file system paths as feed sources. Local
paths are zero-overhead: no HTTP server, no auth, no quotas. The whole feed is a
folder.

## Where it lives

```
C:\code\FrenchExDev.Net\FrenchExDev.Net_i2\FrenchExDev.Net\__Local_Nuget_Registry__
```

The path is **fixed** — code, scripts, and `nuget.config` files reference it
absolutely. Moving the registry requires updating every reference.
