# FrenchExDev Monorepo — Claude Context

Monorepo for FrenchExDev libraries, tools, and developer infrastructure — spanning .NET libraries,
Roslyn source generators, PowerShell automation modules, and homelab orchestration.

## Workspace

[`FrenchExDev_i2.code-workspace`](FrenchExDev_i2.code-workspace) is a VS Code multi-root workspace
with three roots:

| Root | Path | Contents |
|------|------|----------|
| `Root` | `.` | Repository root |
| `Net` | `Net/FrenchExDev/` | 44 .NET packages |
| `PoSh` | `PoSh/FrenchExDev/` | 43 PowerShell modules |

## Layout

```
FrenchExDev_i2/
├── Net/FrenchExDev/     .NET libraries, source generators, CLI tools (44 packages)
├── PoSh/FrenchExDev/    PowerShell automation modules (43 modules)
├── Skills/Net/          Claude Code skill definitions (reusable patterns + docs)
├── Doc/                 Cross-cutting documentation
└── Plans/               Project plans
```

## Where to look for what

| Working in... | Read first |
|---|---|
| Any .NET package | [`Net/FrenchExDev/CLAUDE.md`](Net/FrenchExDev/CLAUDE.md) then `<Package>/CLAUDE.md` |
| Cross-cutting skills | [`Skills/Net/Programming/INDEX.md`](Skills/Net/Programming/INDEX.md) |
| Full project overview | [`README.md`](README.md) |

## Key configuration files (`Net/FrenchExDev/`)

- [`Directory.Packages.props`](Net/FrenchExDev/Directory.Packages.props) — Central Package Management. Never add `Version=` to `<PackageReference>` in `.csproj`.
- [`global.json`](Net/FrenchExDev/global.json) — .NET SDK 10.0.200-preview (`NETSDK1057` warning is normal).
- [`FrenchExDev.Net.slnx`](Net/FrenchExDev/FrenchExDev.Net.slnx) — central solution (all 44 packages).

## Build/test commands

```
dotnet build Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx
dotnet test  Net/FrenchExDev/<Package>/FrenchExDev.Net.<Package>.slnx
```
