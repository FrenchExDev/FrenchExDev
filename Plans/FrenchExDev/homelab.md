# HomeLab Solution — Implementation Plan

## Context

You have a typed .NET ecosystem (Result, Builder, Podman, PodmanCompose, DockerCompose.Bundle, and soon PiHole). The goal is a **code-first homelab management platform** — a daemon with HTTPS API, CLI, and (future) web dashboard — where machines, stacks, certificates, DNS, and runners are described as C# models, version-controlled in git, and reconciled against actual state.

Think of it as "your own Portainer/CasaOS, but typed, SOLID, and designed to grow."

**Key architecture decisions:**
- **Daemon + API**: ASP.NET Core service running on the host, HTTPS API for all management ops
- **CLI**: Thin shell consuming `.Lib` — can also talk to the daemon API
- **`.Lib`**: Application layer shared by CLI and API — composition root, DI, use cases
- **Git-backed config**: All config in `~/.homelab/` git repo, self-hosted on the homelab's own GitLab
- **Machine providers**: Podman machines (primary), SSH machines (remote), extensible to LXC/VMs
- **Multiple DNS zones**: v1 and v2 side-by-side via Pi-hole (confirmed: per-record API, not wildcard)
- **Infrastructure slicing**: each external dependency gets its own project (`.Infrastructure.Podman`, `.Infrastructure.PiHole`, etc.)
- **Multiple runners**: Per-runner machine assignment, executor, tags
- **Project-level SOLID**: DDD, Clean Architecture, real project separation

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│ HOST (Windows / Linux, 64GB RAM)                                        │
│                                                                         │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │ HomeLab Daemon (ASP.NET Core service)                              │ │
│  │  :9443 HTTPS API                                                   │ │
│  │                                                                    │ │
│  │  ┌─────────┐  ┌──────────┐  ┌─────────┐  ┌─────────┐               │ │
│  │  │Machines │  │  Stacks  │  │   PKI   │  │   DNS   │  ...          │ │
│  │  │ API     │  │  API     │  │  API    │  │  API    │               │ │
│  │  └────┬────┘  └────┬─────┘  └────┬────┘  └────┬────┘               │ │
│  │       └─────────────┴─────────────┴─────────────┘                  │ │
│  │                          │                                         │ │
│  │                   HomeLab.Lib (application layer)                  │ │
│  │                          │                                         │ │
│  │              ┌───────────┴───────────┐                             │ │
│  │              │  HomeLab (domain)     │                             │ │
│  │              └───────────┬───────────┘                             │ │
│  │              ┌───────────┴───────────┐                             │ │
│  │              │  HomeLab.Infra        │                             │ │
│  │              └───────────────────────┘                             │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                         │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │ Podman Machine: "homelab" ──── Traefik, GitLab, Keycloak, MinIO  │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │ Podman Machine: "homelab-ci" ──── shell-runner, docker-runner    │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │ SSH Machine: "remote-build" ──── heavy-runner (remote host)      │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  Podman Machine: "default" ← untouched                                  │
│                                                                         │
│  ~/.homelab/ (git repo)                                                 │
│    config/homelab.yml, pki/, secrets/, generated/                       │
│    remote: gitlab.homelab.local:homelab/config.git                      │
│                                                                         │
│  Pi-hole ← DNS zones: .homelab.local, .v1.homelab.local (per-record)    │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 1. Prerequisite: Augment IHttpClient

Add `SendAsync`, `PostAsync`, `DeleteAsync` to the existing interface + wrapper + `FakeHttpClient`.

**Files to modify:**
- `HttpClient/src/FrenchExDev.Net.HttpClient/Code.cs`
- `HttpClient/src/FrenchExDev.Net.HttpClient.Testing/Fakes.cs`

---

## 2. PiHole Solution (standalone, reusable)

```
PiHole/
├── FrenchExDev.Net.PiHole.slnx
├── src/
│   ├── FrenchExDev.Net.PiHole/              # Pi-hole v6 REST client (IHttpClient-based)
│   └── FrenchExDev.Net.PiHole.Testing/      # FakeDnsHostClient, FakeDnsCnameClient
└── test/
    └── FrenchExDev.Net.PiHole.Tests/
```

API: `PiHoleClient` → `IDnsHostClient` (list/add/remove) + `IDnsCnameClient`. Session-based auth (SID cookie + CSRF token). All returns `Result<T>`.

### Pi-hole v6 API — Corrected Endpoints (confirmed via research)

The v6 API uses **URL-encoded path parameters**, not JSON body:

| Operation | Method | Endpoint |
|-----------|--------|----------|
| Auth | POST | `/api/auth` → `{"password":"..."}` |
| List hosts | GET | `/api/config/dns/hosts` |
| Add host | PUT | `/api/config/dns/hosts/{IP}%20{domain}` |
| Delete host | DELETE | `/api/config/dns/hosts/{IP}%20{domain}` |
| List CNAMEs | GET | `/api/config/dns/cnameRecords` |
| Add CNAME | PUT | `/api/config/dns/cnameRecords/{alias},{target},{ttl}` |
| Delete CNAME | DELETE | `/api/config/dns/cnameRecords/{alias},{target},{ttl}` |

**Multi-zone DNS confirmed**: Each hostname is an independent record. `gitlab.homelab.local → 192.168.1.100` and `gitlab.v1.homelab.local → 192.168.1.101` are just two separate PUT calls. No wildcard/dnsmasq needed — explicit records via API are cleaner and fully controllable.

Sources:
- [Custom DNS via REST API - V6](https://discourse.pi-hole.net/t/custom-dns-via-rest-api-v6/78378)
- [Scope of the v6 API includes DNS/CNAME CRUD](https://discourse.pi-hole.net/t/scope-of-the-v6-api-includes-dns-cname-crud/73373)
- [Pi-hole v6: Setting up wildcard domains](https://dev.to/makewithyoshi/pi-hole-v6-setting-up-wildcard-domains-3pbe)

---

## 3. HomeLab Solution — Project Structure

### Bounded Contexts + Shared Kernel

The domain is sliced into bounded contexts. Each is a project with its own models and logic. The **Shared Kernel** holds cross-cutting types that multiple contexts agree on: config model, domain interfaces, shared value objects.

```
HomeLab/
├── FrenchExDev.Net.HomeLab.slnx
├── quality-gate.yml
├── doc/
├── src/
│   │ ── MESSAGING ──────────────────────────────────────────────
│   ├── FrenchExDev.Net.HomeLab.Domain.Messaging/             # Contracts: IDomainEvent, ICommand, IQuery (→ MediatR.Contracts)
│   │
│   │ ── SHARED KERNEL ──────────────────────────────────────────
│   ├── FrenchExDev.Net.HomeLab/                              # Config, interfaces, events, value objects, constants (→ Domain.Messaging)
│   │
│   │ ── DOMAIN BOUNDED CONTEXTS ────────────────────────────────
│   ├── FrenchExDev.Net.HomeLab.Domain.Machines/              # Machine reconciliation + command handlers
│   ├── FrenchExDev.Net.HomeLab.Domain.Dns/                   # DNS zone reconciliation + command handlers
│   ├── FrenchExDev.Net.HomeLab.Domain.Stacks/                # Stack builders (Ingress, Core, Identity, Ci, Storage)
│   ├── FrenchExDev.Net.HomeLab.Domain.Operations/            # Orchestrator + deployment planning
│   ├── FrenchExDev.Net.HomeLab.Domain.Validation/            # Cross-cutting validation
│   │
│   │ ── INFRASTRUCTURE SLICES ──────────────────────────────────
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.Messaging/     # MediatR runtime, pipeline behaviors, event bus
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.Podman/        # IMachineProvider → Podman
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.PodmanCompose/ # IStackDeployer + IRunnerRegistrar
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.PiHole/        # IDnsResolver → PiHole
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.Pki/           # ICertificateAuthority + ITrustStoreManager
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.Git/           # IConfigRepository → git CLI
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.Ssh/           # IMachineProvider → SSH
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.FileSystem/    # IDnsResolver (hosts-file) + ISecretsManager
│   │
│   │ ── APPLICATION + HOSTS ────────────────────────────────────
│   ├── FrenchExDev.Net.HomeLab.Lib/                          # DI, use cases, shared composition root
│   ├── FrenchExDev.Net.HomeLab.Cli/                          # CLI (thin)
│   └── FrenchExDev.Net.HomeLab.Api/                          # Daemon + HTTPS API (thin)
├── test/
│   ├── FrenchExDev.Net.HomeLab.Testing/                      # Fakes for all shared kernel interfaces
│   │
│   │ ── DOMAIN TESTS ──────────────────────────────────────────
│   ├── FrenchExDev.Net.HomeLab.Domain.Messaging.Tests/
│   ├── FrenchExDev.Net.HomeLab.Domain.Machines.Tests/
│   ├── FrenchExDev.Net.HomeLab.Domain.Dns.Tests/
│   ├── FrenchExDev.Net.HomeLab.Domain.Stacks.Tests/
│   ├── FrenchExDev.Net.HomeLab.Domain.Operations.Tests/
│   ├── FrenchExDev.Net.HomeLab.Domain.Validation.Tests/
│   │
│   │ ── INFRASTRUCTURE TESTS ──────────────────────────────────
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.Messaging.Tests/
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.Podman.Tests/
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.PodmanCompose.Tests/
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.PiHole.Tests/
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.Pki.Tests/
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.Git.Tests/
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.Ssh.Tests/
│   ├── FrenchExDev.Net.HomeLab.Infrastructure.FileSystem.Tests/
│   │
│   │ ── APPLICATION + HOST TESTS ──────────────────────────────
│   ├── FrenchExDev.Net.HomeLab.Lib.Tests/                    # DI wiring, mediator dispatch
│   ├── FrenchExDev.Net.HomeLab.Cli.Tests/                    # Command parsing, output formatting
│   └── FrenchExDev.Net.HomeLab.Api.Tests/                    # Endpoint routing, WebApplicationFactory
```

**36 projects.** 1 messaging contracts + 1 shared kernel + 5 domain + 8 infra + 1 lib + 1 cli + 1 api + 1 apphost + 1 testing + 6 domain tests + 8 infra tests + 2 app tests.

### Dependency Graph

```
  Cli ──► Lib ◄── Api
            │
     ┌──────┴──────────────────────────────┐
     ▼                                      ▼
  Domain.*  (6 projects)         Infrastructure.* (8 projects)
     │                                      │
     ▼                                      ▼
  HomeLab  (shared kernel)            HomeLab + one external each
     │
     ▼
  Domain.Messaging  ← MediatR.Contracts + Result
     │
     ▼
  Result, Builder, Bundle

  Domain.Messaging ──► MediatR.Contracts, Result
  HomeLab (kernel) ──► Domain.Messaging, Result, Builder, Bundle
  Each Domain.*    ──► HomeLab (shared kernel only)
  Each Infra.*     ──► HomeLab (shared kernel only) + one external
  Infra.Messaging  ──► HomeLab + Domain.Messaging + MediatR (runtime)
  Lib              ──► HomeLab + ALL Domain.* + ALL Infrastructure.*
  Testing          ──► HomeLab (shared kernel only)

  Each Domain.*.Tests ──► its Domain.* + Testing + shared kernel
  Each Infra.*.Tests  ──► its Infra.* + Testing + shared kernel
  Lib.Tests           ──► Lib + Testing
  Cli.Tests           ──► Cli + Testing
  Api.Tests           ──► Api + Lib + Testing (WebApplicationFactory)
```

### 3.1 Project References

**HomeLab.csproj (DOMAIN)** — zero infrastructure deps
```xml
<ProjectReference Include="..\..\Result\.../FrenchExDev.Net.Result.csproj" />
<ProjectReference Include="..\..\Builder/.../FrenchExDev.Net.Builder.csproj" />
<ProjectReference Include="..\..\DockerCompose/.../FrenchExDev.Net.DockerCompose.Bundle.csproj" />
<PackageReference Include="YamlDotNet" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
```

**Infrastructure slice projects** — each depends on domain + one external:

| Project | Domain Interface | External Dependency |
|---------|-----------------|-------------------|
| `.Infrastructure.Podman` | `IMachineProvider` | `FrenchExDev.Net.Podman` |
| `.Infrastructure.PodmanCompose` | `IStackDeployer`, `IRunnerRegistrar` | `FrenchExDev.Net.PodmanCompose` |
| `.Infrastructure.PiHole` | `IDnsResolver` | `FrenchExDev.Net.PiHole` |
| `.Infrastructure.Pki` | `ICertificateAuthority`, `ITrustStoreManager` | BCL (`System.Security.Cryptography`) |
| `.Infrastructure.Git` | `IConfigRepository` | git CLI (via `Process`) |
| `.Infrastructure.Ssh` | `IMachineProvider` | ssh CLI (via `Process`) |
| `.Infrastructure.FileSystem` | `IDnsResolver` (hosts-file), `ISecretsManager` | filesystem only |

Each `.csproj` references only `HomeLab` (domain) + its one external package. No cross-references between infra slices.

**HomeLab.Lib.csproj** — wires domain + ALL infrastructure slices, exposes use cases
```xml
<ProjectReference Include="../FrenchExDev.Net.HomeLab/..." />
<ProjectReference Include="../FrenchExDev.Net.HomeLab.Infrastructure.Podman/..." />
<ProjectReference Include="../FrenchExDev.Net.HomeLab.Infrastructure.PodmanCompose/..." />
<ProjectReference Include="../FrenchExDev.Net.HomeLab.Infrastructure.PiHole/..." />
<ProjectReference Include="../FrenchExDev.Net.HomeLab.Infrastructure.Pki/..." />
<ProjectReference Include="../FrenchExDev.Net.HomeLab.Infrastructure.Git/..." />
<ProjectReference Include="../FrenchExDev.Net.HomeLab.Infrastructure.Ssh/..." />
<ProjectReference Include="../FrenchExDev.Net.HomeLab.Infrastructure.FileSystem/..." />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" />
<PackageReference Include="Microsoft.Extensions.Logging" />
```

**HomeLab.Cli.csproj** — thin CLI shell
```xml
<ProjectReference Include="../FrenchExDev.Net.HomeLab.Lib/..." />
<PackageReference Include="System.CommandLine" />
<PackageReference Include="Spectre.Console" />
```

**HomeLab.Api.csproj** — ASP.NET Core daemon
```xml
<Sdk>Microsoft.NET.Sdk.Web</Sdk>
<ProjectReference Include="../FrenchExDev.Net.HomeLab.Lib/..." />
<!-- ASP.NET Core is implicit with Web SDK -->
```

---

## 4. Shared Kernel: `FrenchExDev.Net.HomeLab`

The Shared Kernel contains everything multiple bounded contexts must agree on: config model, domain interfaces, value objects, constants, and **domain events**. No business logic — just contracts, data shapes, and things that happened.

### 4.0 Messaging Architecture — Events + CQRS via MediatR

Two new projects power all communication between bounded contexts:

**`HomeLab.Domain.Messaging`** — Contracts library. References `MediatR.Contracts` (interfaces only — `INotification`, `IRequest<T>`). Defines our domain-specific abstractions on top. **No implementation, no MediatR runtime.**

**`HomeLab.Infrastructure.Messaging`** — MediatR runtime + pipeline behaviors (logging, validation, timing, audit). The only project that references the `MediatR` package.

```
HomeLab.Domain.Messaging (NEW)        → MediatR.Contracts, Result
HomeLab.Infrastructure.Messaging (NEW) → MediatR, Domain.Messaging, HomeLab (shared kernel)
HomeLab (Shared Kernel)                → Domain.Messaging (for base types + event definitions)
```

#### `HomeLab.Domain.Messaging` — Contracts

```csharp
// ── Domain Events ──────────────────────────────────────────
/// Our marker extending MediatR's INotification
public interface IDomainEvent : INotification
{
    DateTimeOffset OccurredAt { get; }
    string CorrelationId { get; }
}

/// Base record — all 40+ domain events inherit from this
public abstract record DomainEvent : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required string CorrelationId { get; init; }
}

// ── CQRS Commands ──────────────────────────────────────────
/// A command that mutates state, always returns Result<T>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

/// Handler for a command
public interface ICommandHandler<in TCommand, TResponse>
    : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

// ── CQRS Queries ───────────────────────────────────────────
/// A query that reads state, always returns Result<T>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

/// Handler for a query
public interface IQueryHandler<in TQuery, TResponse>
    : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;

// ── Event Bus ──────────────────────────────────────────────
/// Publishes domain events (abstracts over IMediator.Publish)
public interface IDomainEventBus
{
    Task PublishAsync(IDomainEvent @event, CancellationToken ct);
    Task PublishAsync(IEnumerable<IDomainEvent> events, CancellationToken ct);
}
```

#### `HomeLab.Infrastructure.Messaging` — MediatR Runtime

```csharp
// DI registration
public static IServiceCollection AddHomeLabMessaging(this IServiceCollection services)
{
    services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
        typeof(HomeLabOrchestrator).Assembly,         // Domain.Operations handlers
        typeof(MachineReconciler).Assembly,            // Domain.Machines handlers
        typeof(DnsZoneReconciler).Assembly,            // Domain.Dns handlers
        // ... all domain assemblies with handlers
    ));

    // Pipeline behaviors (executed in order for every request)
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TimingBehavior<,>));

    services.AddSingleton<IDomainEventBus, MediatRDomainEventBus>();
    return services;
}

// Pipeline behaviors
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    // Logs: command/query name, duration, success/failure
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    // Runs IValidator<TRequest> if registered, short-circuits on failure
public class TimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    // Warns if handler takes > 500ms

// Event bus adapter
public class MediatRDomainEventBus(IMediator mediator) : IDomainEventBus
{
    public Task PublishAsync(IDomainEvent @event, CancellationToken ct)
        => mediator.Publish(@event, ct);
}
```

#### CQRS — Commands & Queries replace Use Cases

The use cases in `HomeLab.Lib/UseCases/` become **command/query records** in the Shared Kernel and **handlers** in the domain projects. MediatR dispatches automatically.

```csharp
// ── In Shared Kernel: Operations/Commands/ ─────────────────
public sealed record DeployCommand(HomeLabConfig Config) : ICommand<DeploymentReport>;
public sealed record TeardownCommand(HomeLabConfig Config) : ICommand<TeardownReport>;
public sealed record GenerateCommand(HomeLabConfig Config) : ICommand<Unit>;

// ── In Shared Kernel: Operations/Queries/ ──────────────────
public sealed record StatusQuery(HomeLabConfig Config) : IQuery<StatusReport>;
public sealed record HealthQuery(HomeLabConfig Config) : IQuery<HealthReport>;

// ── In Shared Kernel: Machines/Commands/ ───────────────────
public sealed record ReconcileMachinesCommand(IReadOnlyList<MachineDefinition> Desired) : ICommand<ReconciliationReport>;
public sealed record AddMachineCommand(MachineDefinition Definition) : ICommand<Unit>;
public sealed record RemoveMachineCommand(string Name, bool Destroy) : ICommand<Unit>;

// ── In Shared Kernel: Machines/Queries/ ────────────────────
public sealed record ListMachinesQuery : IQuery<IReadOnlyList<MachineState>>;
public sealed record PlanMachinesQuery(IReadOnlyList<MachineDefinition> Desired) : IQuery<ReconciliationPlan>;

// ── In Shared Kernel: Dns/Commands/ ────────────────────────
public sealed record ApplyDnsCommand(DnsConfig Config) : ICommand<DnsReconciliationPlan>;
public sealed record RemoveDnsCommand(string? Zone) : ICommand<Unit>;

// ── In Shared Kernel: Pki/Commands/ ────────────────────────
public sealed record InitPkiCommand(PkiConfig Config) : ICommand<CaBundle>;
public sealed record TrustCaCommand(CaBundle Ca) : ICommand<Unit>;
public sealed record RenewCertsCommand(PkiConfig Config) : ICommand<ServiceCertBundle>;

// ── In Shared Kernel: Stacks/Commands/ ─────────────────────
public sealed record DeployStackCommand(string StackName, HomeLabConfig Config) : ICommand<Unit>;
public sealed record TeardownStackCommand(string StackName) : ICommand<Unit>;

// ── In Shared Kernel: Runners/Commands/ ────────────────────
public sealed record RegisterRunnerCommand(RunnerDefinition Runner, string GitLabUrl, string Token) : ICommand<Unit>;
public sealed record RegisterAllRunnersCommand(string Token) : ICommand<Unit>;

// ── In Shared Kernel: Config/Commands/ ─────────────────────
public sealed record CommitConfigCommand(string Message) : ICommand<string>;   // Returns commit hash
public sealed record PushConfigCommand : ICommand<Unit>;
public sealed record PullConfigCommand : ICommand<Unit>;

// Handlers live in their respective Domain.* projects
// MediatR auto-discovers and dispatches
```

**What this changes in `HomeLab.Lib`**: The `UseCases/` folder is eliminated. `HomeLabServices.AddHomeLab()` registers MediatR (via `AddHomeLabMessaging()`), which auto-discovers all handlers. The CLI and API call `IMediator.Send(command)` instead of resolving use cases.

```csharp
// CLI (before): var useCase = sp.GetRequiredService<DeployUseCase>(); await useCase.ExecuteAsync(config);
// CLI (after):  var mediator = sp.GetRequiredService<IMediator>(); await mediator.Send(new DeployCommand(config));

// API (before): var useCase = sp.GetRequiredService<MachineUseCases>(); await useCase.AddAsync(def);
// API (after):  await mediator.Send(new AddMachineCommand(def));
```

### 4.0.1 Domain Events Catalog

#### Machine Events (`Machines/Events/`)

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `MachineCreated` | `IMachineProvider.CreateAsync` | Name, Provider, Cpus, MemoryMiB, DiskSizeGiB |
| `MachineStarted` | `IMachineProvider.StartAsync` | Name, Provider |
| `MachineStopped` | `IMachineProvider.StopAsync` | Name, Provider |
| `MachineDestroyed` | `IMachineProvider.DestroyAsync` | Name, Provider |
| `MachineResourcesUpdated` | `IMachineProvider.UpdateAsync` | Name, OldCpus→NewCpus, OldMemory→NewMemory |
| `MachineReconciliationPlanned` | `IMachineReconciler.PlanAsync` | Actions[] (what will happen) |
| `MachineReconciliationApplied` | `IMachineReconciler.ApplyAsync` | Actions[], SuccessCount, FailureCount |

```csharp
public sealed record MachineCreated : DomainEvent
{
    public required string MachineName { get; init; }
    public required string Provider { get; init; }
    public required int Cpus { get; init; }
    public required int MemoryMiB { get; init; }
    public required int DiskSizeGiB { get; init; }
}

public sealed record MachineStarted : DomainEvent
{
    public required string MachineName { get; init; }
    public required string Provider { get; init; }
}

public sealed record MachineStopped : DomainEvent
{
    public required string MachineName { get; init; }
    public required string Provider { get; init; }
}

public sealed record MachineDestroyed : DomainEvent
{
    public required string MachineName { get; init; }
    public required string Provider { get; init; }
}

public sealed record MachineResourcesUpdated : DomainEvent
{
    public required string MachineName { get; init; }
    public int? OldCpus { get; init; }
    public int? NewCpus { get; init; }
    public int? OldMemoryMiB { get; init; }
    public int? NewMemoryMiB { get; init; }
}

public sealed record MachineReconciliationPlanned : DomainEvent
{
    public required IReadOnlyList<MachineAction> Actions { get; init; }
}

public sealed record MachineReconciliationApplied : DomainEvent
{
    public required IReadOnlyList<MachineAction> Actions { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailureCount { get; init; }
}
```

#### DNS Events (`Dns/Events/`)

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `DnsRecordAdded` | `IDnsResolver.AddHostAsync` | Hostname, Ip, Zone |
| `DnsRecordRemoved` | `IDnsResolver.RemoveHostAsync` | Hostname, Zone |
| `DnsRecordUpdated` | `IDnsResolver.UpdateHostAsync` | Hostname, OldIp, NewIp, Zone |
| `DnsZoneReconciled` | `DnsZoneReconciler` | Zone, Added[], Removed[], Unchanged[] |

```csharp
public sealed record DnsRecordAdded : DomainEvent
{
    public required string Hostname { get; init; }
    public required string Ip { get; init; }
    public required string Zone { get; init; }
}

public sealed record DnsRecordRemoved : DomainEvent
{
    public required string Hostname { get; init; }
    public required string Zone { get; init; }
}

public sealed record DnsRecordUpdated : DomainEvent
{
    public required string Hostname { get; init; }
    public required string OldIp { get; init; }
    public required string NewIp { get; init; }
    public required string Zone { get; init; }
}

public sealed record DnsZoneReconciled : DomainEvent
{
    public required string Zone { get; init; }
    public required int AddedCount { get; init; }
    public required int RemovedCount { get; init; }
    public required int UnchangedCount { get; init; }
}
```

#### PKI Events (`Pki/Events/`)

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `RootCaGenerated` | `ICertificateAuthority.EnsureRootCaAsync` | Thumbprint, CommonName, ExpiresAt |
| `RootCaLoaded` | `ICertificateAuthority.EnsureRootCaAsync` | Thumbprint (existing, not regenerated) |
| `ServiceCertIssued` | `ICertificateAuthority.IssueServiceCertAsync` | CommonName, SANs[], ExpiresAt, SignedBy |
| `ServiceCertRenewed` | Pki use case | CommonName, OldThumbprint, NewThumbprint, ExpiresAt |
| `CertExpiringWarning` | `IHealthWatcher` | CertName, DaysRemaining, ExpiresAt |
| `RootCaTrusted` | `ITrustStoreManager.InstallRootCaAsync` | Thumbprint |
| `RootCaUntrusted` | `ITrustStoreManager.RemoveRootCaAsync` | Thumbprint |

```csharp
public sealed record RootCaGenerated : DomainEvent
{
    public required string Thumbprint { get; init; }
    public required string CommonName { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed record RootCaLoaded : DomainEvent
{
    public required string Thumbprint { get; init; }
}

public sealed record ServiceCertIssued : DomainEvent
{
    public required string CommonName { get; init; }
    public required IReadOnlyList<string> SubjectAlternativeNames { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string SignedByThumbprint { get; init; }
}

public sealed record ServiceCertRenewed : DomainEvent
{
    public required string CommonName { get; init; }
    public required string OldThumbprint { get; init; }
    public required string NewThumbprint { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed record CertExpiringWarning : DomainEvent
{
    public required string CertName { get; init; }
    public required int DaysRemaining { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed record RootCaTrusted : DomainEvent
{
    public required string Thumbprint { get; init; }
}

public sealed record RootCaUntrusted : DomainEvent
{
    public required string Thumbprint { get; init; }
}
```

#### Stack Events (`Stacks/Events/`)

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `StackComposeGenerated` | `IStackDefinition.BuildComposeFileAsync` | StackName, MachineName, ServiceCount |
| `StackDeployed` | `IStackDeployer.DeployAsync` | StackName, MachineName, Duration |
| `StackTornDown` | `IStackDeployer.TeardownAsync` | StackName, MachineName |
| `StackHealthChanged` | `IHealthMonitor` | StackName, OldStatus, NewStatus |

```csharp
public sealed record StackComposeGenerated : DomainEvent
{
    public required string StackName { get; init; }
    public required string MachineName { get; init; }
    public required int ServiceCount { get; init; }
}

public sealed record StackDeployed : DomainEvent
{
    public required string StackName { get; init; }
    public required string MachineName { get; init; }
    public required TimeSpan Duration { get; init; }
}

public sealed record StackTornDown : DomainEvent
{
    public required string StackName { get; init; }
    public required string MachineName { get; init; }
}

public sealed record StackHealthChanged : DomainEvent
{
    public required string StackName { get; init; }
    public required HealthStatus OldStatus { get; init; }
    public required HealthStatus NewStatus { get; init; }
}
```

#### Runner Events (`Runners/Events/`)

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `RunnerRegistered` | `IRunnerRegistrar.RegisterAsync` | RunnerName, MachineName, Executor, Tags[] |
| `RunnerUnregistered` | `IRunnerRegistrar.UnregisterAsync` | RunnerName, MachineName |

```csharp
public sealed record RunnerRegistered : DomainEvent
{
    public required string RunnerName { get; init; }
    public required string MachineName { get; init; }
    public required string Executor { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
}

public sealed record RunnerUnregistered : DomainEvent
{
    public required string RunnerName { get; init; }
    public required string MachineName { get; init; }
}
```

#### Config Events (`ConfigRepo/Events/`)

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `ConfigLoaded` | `HomeLabConfig.Load` | SchemaVersion, Path |
| `ConfigValidated` | `HomeLabValidator` | (success) |
| `ConfigValidationFailed` | `HomeLabValidator` | Errors[] |
| `ConfigCommitted` | `IConfigRepository.CommitAsync` | Message, CommitHash |
| `ConfigPushed` | `IConfigRepository.PushAsync` | Remote, CommitHash |
| `ConfigPulled` | `IConfigRepository.PullAsync` | CommitsBehind |
| `ConfigMigrated` | `IConfigMigrator` | FromVersion, ToVersion |
| `ConfigMutated` | Config mutation use cases | Field, OldValue, NewValue (e.g., "machines[2].cpus", "4", "8") |

```csharp
public sealed record ConfigLoaded : DomainEvent
{
    public required int SchemaVersion { get; init; }
    public required string Path { get; init; }
}

public sealed record ConfigValidated : DomainEvent;

public sealed record ConfigValidationFailed : DomainEvent
{
    public required IReadOnlyList<string> Errors { get; init; }
}

public sealed record ConfigCommitted : DomainEvent
{
    public required string Message { get; init; }
    public required string CommitHash { get; init; }
}

public sealed record ConfigPushed : DomainEvent
{
    public required string Remote { get; init; }
    public required string CommitHash { get; init; }
}

public sealed record ConfigPulled : DomainEvent
{
    public required int CommitsBehind { get; init; }
}

public sealed record ConfigMigrated : DomainEvent
{
    public required int FromVersion { get; init; }
    public required int ToVersion { get; init; }
}

public sealed record ConfigMutated : DomainEvent
{
    public required string Field { get; init; }
    public required string? OldValue { get; init; }
    public required string? NewValue { get; init; }
    public required string Source { get; init; }   // "cli", "api", "gitops"
}
```

#### Secrets Events (`Secrets/Events/`)

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `SecretsGenerated` | `ISecretsManager.GenerateAsync` | Names[] (never values!) |
| `SecretAccessed` | `ISecretsManager.LoadSecretAsync` | Name (audit trail) |

```csharp
public sealed record SecretsGenerated : DomainEvent
{
    public required IReadOnlyList<string> SecretNames { get; init; }
}

public sealed record SecretAccessed : DomainEvent
{
    public required string SecretName { get; init; }
}
```

#### Operations Events (`Operations/Events/`)

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `DeploymentStarted` | `IHomeLabOrchestrator` | PlanStepCount |
| `DeploymentStepCompleted` | `IHomeLabOrchestrator` | StepName, StepIndex, TotalSteps, Duration |
| `DeploymentCompleted` | `IHomeLabOrchestrator` | TotalDuration, StepsSucceeded, StepsFailed |
| `DeploymentFailed` | `IHomeLabOrchestrator` | FailedStep, Error |
| `TeardownStarted` | `IHomeLabOrchestrator` | — |
| `TeardownCompleted` | `IHomeLabOrchestrator` | TotalDuration |
| `HealthCheckCompleted` | `IHealthMonitor` | HealthyCount, UnhealthyCount |

```csharp
public sealed record DeploymentStarted : DomainEvent
{
    public required int PlanStepCount { get; init; }
}

public sealed record DeploymentStepCompleted : DomainEvent
{
    public required string StepName { get; init; }
    public required int StepIndex { get; init; }
    public required int TotalSteps { get; init; }
    public required TimeSpan Duration { get; init; }
}

public sealed record DeploymentCompleted : DomainEvent
{
    public required TimeSpan TotalDuration { get; init; }
    public required int StepsSucceeded { get; init; }
    public required int StepsFailed { get; init; }
}

public sealed record DeploymentFailed : DomainEvent
{
    public required string FailedStep { get; init; }
    public required string Error { get; init; }
}

public sealed record TeardownStarted : DomainEvent;

public sealed record TeardownCompleted : DomainEvent
{
    public required TimeSpan TotalDuration { get; init; }
}

public sealed record HealthCheckCompleted : DomainEvent
{
    public required int HealthyCount { get; init; }
    public required int UnhealthyCount { get; init; }
    public required int DegradedCount { get; init; }
}
```

#### Bootstrap Events (`Bootstrap/Events/`)

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `BootstrapApiKeyGenerated` | Init use case | (key hash, never the key itself) |
| `AuthModeChanged` | Auth strategy | OldMode, NewMode (apikey → oidc) |

```csharp
public sealed record BootstrapApiKeyGenerated : DomainEvent
{
    public required string KeyHash { get; init; }
}

public sealed record AuthModeChanged : DomainEvent
{
    public required string OldMode { get; init; }
    public required string NewMode { get; init; }
}
```

### 4.0.2 Event Flow — Who Handles What

Events flow from producers (domain/infra) through the event bus to consumers:

| Handler | Subscribes To | Action |
|---------|--------------|--------|
| **AuditLogHandler** | ALL events | Writes to `audit.jsonl` |
| **NotificationHandler** | `StackHealthChanged`, `CertExpiringWarning`, `DeploymentFailed`, `MachineDestroyed` | Sends alerts (Gotify, Slack, email) |
| **DashboardPushHandler** | ALL events | Pushes via SignalR to connected dashboard clients |
| **ConfigAutoCommitHandler** | `ConfigMutated` | `git commit -m "{source}: {field} changed"` |
| **LoggingHandler** | ALL events | Structured log entry per event |
| **DeploymentProgressHandler** | `DeploymentStepCompleted` | Updates CLI progress bar / API SSE stream |

**Handler locations**:
- `AuditLogHandler` → `HomeLab.Infrastructure.FileSystem`
- `NotificationHandler` → `HomeLab.Infrastructure.Monitoring` (future)
- `DashboardPushHandler` → `HomeLab.Api`
- `ConfigAutoCommitHandler` → `HomeLab.Infrastructure.Git`
- `LoggingHandler` → `HomeLab.Lib`
- `DeploymentProgressHandler` → `HomeLab.Lib` (CLI) / `HomeLab.Api` (SSE)

### 4.0.3 Shared Kernel Layout (updated with Events)

```
FrenchExDev.Net.HomeLab (Shared Kernel)
├── Events/
│   ├── IDomainEvent.cs
│   ├── DomainEvent.cs                 # Base record
│   ├── IDomainEventBus.cs
│   └── IDomainEventHandler.cs
│
├── Config/
│   ├── HomeLabConfig.cs
│   ├── ...sub-configs...
│   └── Events/
│       ├── ConfigLoaded.cs
│       ├── ConfigValidated.cs
│       ├── ConfigValidationFailed.cs
│       ├── ConfigCommitted.cs
│       ├── ConfigPushed.cs
│       ├── ConfigPulled.cs
│       ├── ConfigMigrated.cs
│       └── ConfigMutated.cs
│
├── Machines/
│   ├── IMachineProvider.cs
│   ├── IMachineReconciler.cs
│   ├── ...value objects...
│   └── Events/
│       ├── MachineCreated.cs
│       ├── MachineStarted.cs
│       ├── MachineStopped.cs
│       ├── MachineDestroyed.cs
│       ├── MachineResourcesUpdated.cs
│       ├── MachineReconciliationPlanned.cs
│       └── MachineReconciliationApplied.cs
│
├── Dns/
│   ├── IDnsResolver.cs
│   ├── ...value objects...
│   └── Events/
│       ├── DnsRecordAdded.cs
│       ├── DnsRecordRemoved.cs
│       ├── DnsRecordUpdated.cs
│       └── DnsZoneReconciled.cs
│
├── Pki/
│   ├── ICertificateAuthority.cs
│   ├── ITrustStoreManager.cs
│   ├── ...value objects...
│   └── Events/
│       ├── RootCaGenerated.cs
│       ├── RootCaLoaded.cs
│       ├── ServiceCertIssued.cs
│       ├── ServiceCertRenewed.cs
│       ├── CertExpiringWarning.cs
│       ├── RootCaTrusted.cs
│       └── RootCaUntrusted.cs
│
├── Stacks/
│   ├── IStackDefinition.cs
│   ├── IStackDeployer.cs
│   └── Events/
│       ├── StackComposeGenerated.cs
│       ├── StackDeployed.cs
│       ├── StackTornDown.cs
│       └── StackHealthChanged.cs
│
├── Runners/
│   ├── IRunnerRegistrar.cs
│   └── Events/
│       ├── RunnerRegistered.cs
│       └── RunnerUnregistered.cs
│
├── Secrets/
│   ├── ISecretsManager.cs
│   └── Events/
│       ├── SecretsGenerated.cs
│       └── SecretAccessed.cs
│
├── ConfigRepo/
│   └── IConfigRepository.cs
│
├── Operations/
│   ├── IHomeLabOrchestrator.cs
│   ├── ...report types...
│   └── Events/
│       ├── DeploymentStarted.cs
│       ├── DeploymentStepCompleted.cs
│       ├── DeploymentCompleted.cs
│       ├── DeploymentFailed.cs
│       ├── TeardownStarted.cs
│       ├── TeardownCompleted.cs
│       └── HealthCheckCompleted.cs
│
├── Bootstrap/
│   └── Events/
│       ├── BootstrapApiKeyGenerated.cs
│       └── AuthModeChanged.cs
│
└── Constants/
    ├── HomeLabNetworks.cs
    └── HomeLabVolumes.cs
```

#### Backup Events (`Backup/Events/`) — NEW CONTEXT

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `BackupStarted` | `IBackupManager.BackupAsync` | StackName, Strategy |
| `BackupCompleted` | `IBackupManager.BackupAsync` | StackName, BackupId, SizeBytes, Duration |
| `BackupFailed` | `IBackupManager.BackupAsync` | StackName, Error |
| `RestoreStarted` | `IBackupManager.RestoreAsync` | StackName, BackupId |
| `RestoreCompleted` | `IBackupManager.RestoreAsync` | StackName, BackupId, Duration |
| `RestoreFailed` | `IBackupManager.RestoreAsync` | StackName, BackupId, Error |
| `BackupPruned` | `IBackupManager.PruneAsync` | StackName, RemovedCount, FreedBytes |
| `BackupScheduleTriggered` | `IBackupScheduler` | StackName, ScheduleCron |

#### Monitoring Events (`Monitoring/Events/`) — NEW CONTEXT

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `HealthWatcherStarted` | `IHealthWatcher.StartAsync` | IntervalSeconds |
| `HealthWatcherStopped` | `IHealthWatcher.StopAsync` | — |
| `ServiceHealthCheckPassed` | `IHealthMonitor` | StackName, ServiceName, ResponseTimeMs |
| `ServiceHealthCheckFailed` | `IHealthMonitor` | StackName, ServiceName, Error |
| `ServiceHealthRecovered` | `IHealthWatcher` (state diff) | StackName, ServiceName, DowntimeDuration |
| `NotificationSent` | `INotificationChannel.SendAsync` | ChannelType, Title, Severity |
| `NotificationFailed` | `INotificationChannel.SendAsync` | ChannelType, Error |
| `ResourceThresholdExceeded` | `IResourceCollector` | MachineName, Resource (cpu/ram/disk), Value, Threshold |

#### Update Events (`Updates/Events/`) — NEW CONTEXT

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `UpdateCheckCompleted` | `IUpdateManager.CheckUpdatesAsync` | AvailableCount |
| `UpdateAvailable` | `IUpdateManager.CheckUpdatesAsync` | Image, CurrentTag, LatestTag |
| `UpdateApplied` | `IUpdateManager.ApplyUpdateAsync` | StackName, FromTag, ToTag, Duration |
| `UpdateRolledBack` | `IUpdateManager.RollbackAsync` | StackName, FromTag, RolledBackToTag, Reason |
| `ImagePinned` | Config mutation | Image, PinnedTag |
| `ImageUnpinned` | Config mutation | Image |

#### Service Catalog Events (`Catalog/Events/`) — NEW CONTEXT

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `CustomStackAdded` | `IServiceCatalog.AddAsync` | Name, Machine, ComposeFile, TraefikHost |
| `CustomStackRemoved` | `IServiceCatalog.RemoveAsync` | Name |
| `CustomStackEnabled` | `IServiceCatalog.EnableAsync` | Name |
| `CustomStackDisabled` | `IServiceCatalog.DisableAsync` | Name |

#### Networking Events (`Networking/Events/`) — NEW CONTEXT

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `NetworkCreated` | Stack deployment | NetworkName, Subnet, MachineName |
| `NetworkRemoved` | Stack teardown | NetworkName, MachineName |
| `PortBindingAllocated` | Stack deployment | Port, Protocol, StackName |
| `PortBindingReleased` | Stack teardown | Port, StackName |

#### Daemon Events (`Daemon/Events/`) — NEW CONTEXT

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `DaemonStarted` | API startup | Port, AuthMode, Version |
| `DaemonStopped` | API shutdown | Uptime |
| `ApiRequestReceived` | Middleware | Method, Path, Actor |
| `ApiRequestCompleted` | Middleware | Method, Path, StatusCode, Duration |
| `GitOpsReconciliationTriggered` | `IConfigWatcher` | Source (file-change / webhook / manual) |
| `GitOpsReconciliationCompleted` | `IConfigWatcher` | ChangesApplied |

#### Additional Events in Existing Contexts

**Machines** (additions):

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `MachineConnectionDiscovered` | `MachineContext` | MachineName, ConnectionUri |
| `MachineHealthChecked` | Health monitor | MachineName, CpuPercent, MemoryPercent |

**DNS** (additions):

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `DnsProviderSwitched` | Config change | OldProvider, NewProvider |
| `DnsResolutionVerified` | Post-apply check | Hostname, ResolvedIp, Expected |

**PKI** (additions):

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `CertCopiedToMachine` | Deploy step | MachineName, CertPath |
| `CertRotationStarted` | Renew use case | CommonName |
| `CertRotationCompleted` | Renew use case | CommonName, NewThumbprint |

**Stacks** (additions):

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `ComposeFileWritten` | Generate step | StackName, FilePath |
| `StackRestarted` | Stack ops | StackName, ServiceName |

**Secrets** (additions):

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `SecretRotated` | Secrets use case | SecretName |
| `SecretsEncrypted` | SOPS encryption | FileCount |
| `SecretsDecrypted` | SOPS decryption (deploy) | FileCount |

**Config** (additions):

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `ConfigRemoteSet` | `IConfigRepository.SetRemoteAsync` | RemoteUrl |
| `ConfigFileChanged` | Filesystem watcher | FilePath |

**Operations** (additions):

| Event | Raised By | Key Data |
|-------|-----------|----------|
| `TeardownFailed` | Orchestrator | FailedStep, Error |
| `StatusCollected` | Status query | MachineCount, StackCount, HealthySummary |

---

**Total domain events: 82** across **14 bounded contexts**.

| Context | Event Count |
|---------|------------|
| Machines | 9 |
| DNS | 6 |
| PKI | 10 |
| Stacks | 6 |
| Runners | 2 |
| Config | 10 |
| Secrets | 5 |
| Operations | 9 |
| Bootstrap | 2 |
| Backup | 8 |
| Monitoring | 8 |
| Updates | 6 |
| Service Catalog | 4 |
| Networking | 4 |
| Daemon | 6 |

**Shared Kernel dependencies**: Result, Builder, DockerCompose.Bundle, YamlDotNet, Logging.Abstractions, Domain.Messaging.

---

## 4b. Domain Bounded Contexts

Each domain project contains **pure logic** — no I/O, no infrastructure. Depends only on the Shared Kernel.

### `HomeLab.Domain.Machines`

Machine reconciliation logic: compare desired definitions against actual state, produce an action plan.

```
├── MachineReconciler.cs               # IMachineReconciler implementation (pure)
│   Routes each MachineDefinition to the correct IMachineProvider by provider type.
│   Diffs desired vs actual: Create / UpdateResources / Start / AlreadyRunning.
│   Never touches unmanaged machines.
└── MachineContextFactory.cs           # Creates per-machine context (connection string)
```

### `HomeLab.Domain.Dns`

DNS zone reconciliation: pure function, no network calls.

```
├── DnsZoneReconciler.cs               # Desired zones + current entries → add/remove/update actions
│   Handles multiple suffixes: .homelab.local + .v1.homelab.local coexist.
│   Filters current entries by managed suffix to avoid touching unrelated records.
└── DnsReconciliationPlanBuilder.cs    # Builds the plan from zone definitions
```

### `HomeLab.Domain.Stacks`

Compose file builders for each built-in service stack. Each produces a `ComposeFile` from config — pure model construction, no deployment.

```
├── StackManifest.cs                   # All stacks + their machine assignments
├── Ingress/
│   └── IngressStack.cs               # IStackDefinition → Traefik ComposeFile
├── Core/
│   ├── CoreStack.cs                   # IStackDefinition → GitLab Omnibus ComposeFile
│   └── OmnibusConfigBuilder.cs        # Generates GITLAB_OMNIBUS_CONFIG Ruby string
├── Identity/
│   └── IdentityStack.cs              # IStackDefinition → Keycloak + PG ComposeFile
├── Ci/
│   └── CiStack.cs                    # IStackDefinition → N runner services (multi-runner)
└── Storage/
    └── StorageStack.cs               # IStackDefinition → MinIO ComposeFile
```

### `HomeLab.Domain.Operations`

The orchestrator: sequences all steps in the correct dependency order. **Pure delegation** — calls through Shared Kernel interfaces only.

```
├── HomeLabOrchestrator.cs             # IHomeLabOrchestrator implementation
│   Deploy order: Secrets → Machines → PKI → Trust → DNS → Stacks → Runners
│   Teardown: reverse. Each step returns Result<T>, short-circuits via Bind.
└── DeploymentPlanBuilder.cs           # Builds ordered plan from config
```

```csharp
public class HomeLabOrchestrator : IHomeLabOrchestrator
{
    public HomeLabOrchestrator(
        IMachineReconciler machines,
        ICertificateAuthority pki,
        ITrustStoreManager trustStore,
        IDnsResolver dns,
        IStackDeployer deployer,
        IRunnerRegistrar runners,
        ISecretsManager secrets,
        IReadOnlyList<IStackDefinition> stacks,
        ILogger<HomeLabOrchestrator>? logger = null);
}
```

### `HomeLab.Domain.Validation`

Cross-cutting validation that spans multiple bounded contexts.

```
└── HomeLabValidator.cs                # Returns Result<HomeLabConfig>
    Validates: port conflicts, machine refs match definitions, runner machines exist,
    zone suffixes unique, secrets required, OIDC consistency, PKI SAN coverage.
```

### 4.2 Machine Provider Abstraction

Machines are no longer Podman-only. The `IMachineProvider` interface supports pluggable backends:

```csharp
public interface IMachineProvider
{
    string ProviderType { get; }  // "podman", "ssh"
    Task<Result<IReadOnlyList<MachineState>>> ListAsync(CancellationToken ct);
    Task<Result<Unit>> CreateAsync(MachineDefinition def, CancellationToken ct);
    Task<Result<Unit>> UpdateAsync(MachineDefinition def, CancellationToken ct);
    Task<Result<Unit>> StartAsync(string name, CancellationToken ct);
    Task<Result<Unit>> StopAsync(string name, CancellationToken ct);
    Task<Result<Unit>> DestroyAsync(string name, CancellationToken ct);
    Task<Result<MachineState>> InspectAsync(string name, CancellationToken ct);
}
```

`IMachineReconciler` works across providers — it routes each `MachineDefinition` to the correct provider based on `definition.Provider`.

```yaml
machines:
  - name: homelab
    provider: podman
    cpus: 4
    memory: 8192
    disk-size: 100
    rootful: true
    purpose: Service host

  - name: homelab-ci
    provider: podman
    cpus: 4
    memory: 4096
    disk-size: 50
    rootful: true
    purpose: CI runners

  - name: remote-build
    provider: ssh
    host: 192.168.1.50
    user: build
    ssh-key-secret: remote-build-ssh-key
    purpose: Remote build machine
```

### 4.3 Git-Backed Config (`IConfigRepository`)

```csharp
public interface IConfigRepository
{
    /// Initialize ~/.homelab/ as a git repo (if not already)
    Task<Result<Unit>> InitAsync(CancellationToken ct);

    /// Commit current config state with a message
    Task<Result<Unit>> CommitAsync(string message, CancellationToken ct);

    /// Push to remote (the homelab's own GitLab)
    Task<Result<Unit>> PushAsync(CancellationToken ct);

    /// Pull latest from remote
    Task<Result<Unit>> PullAsync(CancellationToken ct);

    /// Set remote URL (e.g., after GitLab is first deployed)
    Task<Result<Unit>> SetRemoteAsync(string url, CancellationToken ct);

    /// Get current status (dirty, ahead, behind)
    Task<Result<ConfigRepoStatus>> StatusAsync(CancellationToken ct);

    /// Get commit log
    Task<Result<IReadOnlyList<ConfigCommit>>> LogAsync(int limit, CancellationToken ct);
}
```

Directory layout:
```
~/.homelab/                       # Git repo root
├── homelab.yml                   # Main config
├── pki/
│   ├── ca.pem, ca-key.pem       # Root CA
│   └── wildcard.pem, wildcard-key.pem
├── secrets/                      # Encrypted (SOPS or git-crypt)
│   ├── gitlab.env
│   ├── keycloak.env
│   └── minio.env
├── generated/                    # .gitignore'd — compose YAMLs
│   ├── ingress.yml
│   ├── core.yml
│   └── ...
└── .gitignore
```

**Self-referential flow**: After initial deploy, GitLab is running. The daemon auto-sets the remote to `git@gitlab.homelab.local:homelab/config.git` and pushes. From then on, config changes are committed and pushed to the homelab's own GitLab.

### 4.4 Multiple DNS Zones

```csharp
public record DnsZone
{
    public required string Suffix { get; init; }    // "homelab.local"
    public required string TargetIp { get; init; }  // "192.168.1.100"
    public IReadOnlyList<string> Hostnames { get; init; } = [];
}
```

### 4.5 Multiple Runners

```csharp
public record RunnerDefinition
{
    public required string Name { get; init; }
    public string Image { get; init; } = "gitlab/gitlab-runner:latest";
    public required string Machine { get; init; }
    public string Executor { get; init; } = "docker";
    public IReadOnlyList<string> Tags { get; init; } = [];
    public int? Limit { get; init; }
}
```

---

## 5. Infrastructure Slices (one project per external dependency)

**`HomeLab.Infrastructure.Podman`**
- `PodmanMachineProvider.cs` — `IMachineProvider` → `Podman.Machine.Init/Set/Start/Stop/List/Inspect`
- `MachineContext.cs` — `BinaryBinding` factory per machine (`--connection`)

**`HomeLab.Infrastructure.PodmanCompose`**
- `PodmanComposeDeployer.cs` — `IStackDeployer` → `PodmanCompose.Up/Down/Ps`
- `PodmanRunnerRegistrar.cs` — `IRunnerRegistrar` → exec into runner container
- `ComposeYamlSerializer.cs` — `ComposeFile` → YAML file

**`HomeLab.Infrastructure.PiHole`**
- `PiHoleDnsResolver.cs` — `IDnsResolver` → `PiHoleClient` (PUT/DELETE per-record API)

**`HomeLab.Infrastructure.Pki`**
- `X509CertificateAuthority.cs` — `ICertificateAuthority` → `CertificateRequest`, RSA-4096
- `X509TrustStoreManager.cs` — `ITrustStoreManager` → `X509Store`

**`HomeLab.Infrastructure.Git`**
- `GitConfigRepository.cs` — `IConfigRepository` → `git init/commit/push/pull/remote` via Process

**`HomeLab.Infrastructure.Ssh`**
- `SshMachineProvider.cs` — `IMachineProvider` → SSH connectivity, remote command exec

**`HomeLab.Infrastructure.FileSystem`**
- `HostsFileDnsResolver.cs` — `IDnsResolver` → managed block in hosts file
- `FileSecretsManager.cs` — `ISecretsManager` → `~/.homelab/secrets/` files

Each implementation is `sealed internal`. Exposed only via `HomeLab.Lib` DI wiring.

---

## 6. Application Layer: `FrenchExDev.Net.HomeLab.Lib`

The **shared composition root**. Both CLI and API consume this. It wires domain interfaces to infrastructure implementations and exposes high-level **use cases**.

```
FrenchExDev.Net.HomeLab.Lib
├── HomeLabServices.cs                  # DI registration: AddHomeLab(IServiceCollection, HomeLabConfig)
│
├── UseCases/
│   ├── InitUseCase.cs                  # Scaffold config, init git repo
│   ├── ValidateUseCase.cs              # Validate config
│   ├── DeployUseCase.cs                # Full pipeline: machines → pki → dns → stacks → runners
│   ├── TeardownUseCase.cs              # Reverse pipeline
│   ├── GenerateUseCase.cs              # Generate compose + certs without deploying
│   ├── StatusUseCase.cs                # Full overview
│   ├── HealthUseCase.cs                # Health checks
│   │
│   ├── MachineUseCases.cs              # Plan, apply, list, status, add, remove
│   ├── PkiUseCases.cs                  # Init, trust, untrust, renew, status
│   ├── DnsUseCases.cs                  # Apply, remove, status, list
│   ├── StackUseCases.cs                # List, deploy, teardown, logs
│   ├── RunnerUseCases.cs               # List, register, unregister
│   ├── SecretUseCases.cs               # Init, list
│   └── ConfigRepoUseCases.cs           # Commit, push, pull, status, log
```

```csharp
// DI registration — called by both CLI and API
public static class HomeLabServices
{
    public static IServiceCollection AddHomeLab(
        this IServiceCollection services, HomeLabConfig config)
    {
        // Domain
        services.AddSingleton(config);
        services.AddSingleton<HomeLabOrchestrator>();
        services.AddSingleton<HomeLabValidator>();

        // Infrastructure — chosen by config
        services.AddSingleton<IMachineProvider, PodmanMachineProvider>();
        services.AddSingleton<IMachineProvider, SshMachineProvider>(); // multi-registration
        services.AddSingleton<IMachineReconciler, MachineReconciler>();

        services.AddSingleton<IDnsResolver>(sp => config.Dns.Provider switch
        {
            "pihole" => new PiHoleDnsResolver(...),
            "hosts-file" => new HostsFileDnsResolver(...),
            _ => throw new InvalidOperationException()
        });

        services.AddSingleton<ICertificateAuthority, X509CertificateAuthority>();
        services.AddSingleton<ITrustStoreManager, X509TrustStoreManager>();
        services.AddSingleton<IStackDeployer, PodmanComposeDeployer>();
        services.AddSingleton<IRunnerRegistrar, PodmanRunnerRegistrar>();
        services.AddSingleton<ISecretsManager, FileSecretsManager>();
        services.AddSingleton<IConfigRepository, GitConfigRepository>();

        // Use cases
        services.AddSingleton<DeployUseCase>();
        services.AddSingleton<MachineUseCases>();
        // ... etc

        return services;
    }
}
```

---

## 7. CLI: `FrenchExDev.Net.HomeLab.Cli`

**Thin shell.** Each command resolves a use case from DI and calls it.

```csharp
// Example: deploy command
var deployCmd = new Command("deploy", "Full deploy pipeline");
deployCmd.SetHandler(async (ctx) =>
{
    var useCase = host.Services.GetRequiredService<DeployUseCase>();
    var result = await useCase.ExecuteAsync(config, ctx.GetCancellationToken());
    result.Match(
        onSuccess: report => RenderDeployReport(report),
        onFailure: errors => RenderErrors(errors));
});
```

### Command Tree

```
homelab
├── init                                        # Scaffold config + git init
├── validate                                    # Validate config
├── status                                      # Full overview
├── health                                      # Health checks
├── deploy [--stack <n>] [--plan-only]          # Full pipeline
├── teardown [--stack <n>] [--destroy-machines] # Reverse
├── generate                                    # Compose YAML + certs (no deploy)
│
├── machines
│   ├── list                    # All machines (managed + unmanaged, all providers)
│   ├── plan                    # Dry-run reconciliation
│   ├── apply                   # Create/update/start
│   ├── add <name>              # Interactive: add a machine definition to config
│   ├── remove <name>           # Remove from config (+ optionally destroy)
│   └── status <name>           # Detailed info
│
├── pki
│   ├── init                    # Generate root CA + service certs
│   ├── trust                   # Install root CA
│   ├── untrust                 # Remove from trust store
│   ├── renew                   # Regenerate service certs (keep CA)
│   └── status                  # Validity, expiration, trust state
│
├── dns
│   ├── apply                   # Register records (all zones)
│   ├── remove [--zone <sfx>]   # Remove managed records
│   ├── status                  # Resolution state per zone
│   └── list                    # All managed records
│
├── stacks
│   ├── list                    # All stacks + status
│   ├── deploy <name>           # Deploy one
│   ├── teardown <name>         # Teardown one
│   └── logs <name>             # Tail logs
│
├── runners
│   ├── list                    # All runners + registration status
│   ├── register <name> --token # Register one
│   ├── register-all --token    # Register all
│   └── unregister <name>       # Unregister
│
├── secrets
│   ├── init                    # Generate random secrets
│   └── list                    # Show names (not values)
│
├── config
│   ├── commit [-m <msg>]       # Git commit current config
│   ├── push                    # Push to homelab GitLab
│   ├── pull                    # Pull latest
│   ├── status                  # Git status (dirty, ahead, behind)
│   ├── log                     # Commit history
│   └── set-remote <url>        # Set GitLab remote
│
└── daemon
    ├── start                   # Start API daemon (foreground or --background)
    ├── stop                    # Stop daemon
    └── status                  # Daemon running? PID? Port?
```

---

## 8. API Daemon: `FrenchExDev.Net.HomeLab.Api`

ASP.NET Core minimal API running as a system service (systemd on Linux, Windows Service on Windows).

### 8.1 API Endpoints

```
POST   /api/deploy                    # Full deploy
POST   /api/teardown                  # Full teardown
GET    /api/status                    # Full status
GET    /api/health                    # Health checks

GET    /api/machines                  # List all machines
POST   /api/machines                  # Add a machine definition
GET    /api/machines/{name}           # Machine detail
PUT    /api/machines/{name}           # Update machine definition
DELETE /api/machines/{name}           # Remove machine
POST   /api/machines/{name}/start     # Start machine
POST   /api/machines/{name}/stop      # Stop machine
POST   /api/machines/reconcile        # Plan + apply

GET    /api/pki/status                # Cert status
POST   /api/pki/init                  # Generate CA + certs
POST   /api/pki/trust                 # Install CA
POST   /api/pki/renew                 # Renew service certs

GET    /api/dns                       # List all records
POST   /api/dns/apply                 # Apply all zones
DELETE /api/dns                       # Remove managed records
GET    /api/dns/zones                 # List zones

GET    /api/stacks                    # List stacks
POST   /api/stacks/{name}/deploy      # Deploy one
POST   /api/stacks/{name}/teardown    # Teardown one
GET    /api/stacks/{name}/logs        # Get logs

GET    /api/runners                   # List runners
POST   /api/runners/{name}/register   # Register
POST   /api/runners/{name}/unregister # Unregister

GET    /api/config/status             # Git status
POST   /api/config/commit             # Commit
POST   /api/config/push               # Push to GitLab
POST   /api/config/pull               # Pull
```

### 8.2 Security

- HTTPS only (uses the homelab's own wildcard cert from PKI)
- Auth via Keycloak OIDC (once Keycloak is deployed) or API key for bootstrap
- Runs on `api.homelab.local:9443` (or configurable port)

### 8.3 Service Registration

```csharp
var builder = WebApplication.CreateBuilder(args);
var config = HomeLabConfig.Load(configPath);
builder.Services.AddHomeLab(config);  // Shared DI from HomeLab.Lib
var app = builder.Build();
app.MapHomeLabEndpoints();            // Registers all /api/* routes
app.Run();
```

---

## 9. Git-Backed Config

### 9.1 Lifecycle

1. `homelab init` → creates `~/.homelab/`, scaffolds `homelab.yml`, `git init`
2. User edits config (manually or via CLI/API: `homelab machines add`)
3. Every mutation auto-commits: `"add machine: homelab-ci"`
4. After GitLab is deployed: `homelab config set-remote git@gitlab.homelab.local:homelab/config.git`
5. `homelab config push` → pushes to self-hosted GitLab
6. From another machine: `git clone` + `homelab deploy` to reproduce the entire homelab

### 9.2 Config Mutations via API

When the dashboard POSTs to `/api/machines` to add a machine:
1. API validates the definition
2. Writes updated `homelab.yml`
3. Commits: `"api: add machine remote-build"`
4. Optionally auto-pushes to GitLab

---

## 10. Areas for Future Expansion

Research shows 64GB homelab servers typically run 30-40 services. The architecture should support growing beyond the initial 5 stacks.

### 10.1 Service Catalog

Allow users to add services beyond the built-in 5 stacks. Any Docker Compose-compatible service can be managed by HomeLab with Traefik routing, DNS registration, and lifecycle management.

**Domain (`HomeLab`)**

```csharp
public record CustomStackDefinition
{
    public required string Name { get; init; }           // "nextcloud"
    public required string Machine { get; init; }        // which machine
    public required string ComposeFile { get; init; }    // relative path in ~/.homelab/stacks/
    public string? TraefikHost { get; init; }            // "cloud.homelab.local" → auto-DNS + labels
    public string? DnsZone { get; init; }                // which zone to register in
    public IReadOnlyList<string> DependsOn { get; init; } = []; // other stack names
    public IReadOnlyList<string> Networks { get; init; } = ["homelab-frontend"];
    public bool Enabled { get; init; } = true;
}

public interface IServiceCatalog
{
    Task<Result<IReadOnlyList<CustomStackDefinition>>> ListAsync(CancellationToken ct);
    Task<Result<Unit>> AddAsync(CustomStackDefinition stack, CancellationToken ct);
    Task<Result<Unit>> RemoveAsync(string name, CancellationToken ct);
    Task<Result<Unit>> EnableAsync(string name, CancellationToken ct);
    Task<Result<Unit>> DisableAsync(string name, CancellationToken ct);
}
```

**Config**
```yaml
custom-stacks:
  - name: nextcloud
    machine: homelab
    compose-file: stacks/nextcloud.yml
    traefik-host: cloud.homelab.local
    depends-on: [core, storage]

  - name: vaultwarden
    machine: homelab
    compose-file: stacks/vaultwarden.yml
    traefik-host: vault.homelab.local

  - name: immich
    machine: homelab
    compose-file: stacks/immich.yml
    traefik-host: photos.homelab.local
    depends-on: [storage]

  - name: jellyfin
    machine: homelab
    compose-file: stacks/jellyfin.yml
    traefik-host: media.homelab.local
    enabled: false
```

**Infrastructure** — `HomeLab.Infrastructure.ServiceCatalog`
- `FileServiceCatalog.cs` — reads/writes custom stack definitions to `~/.homelab/stacks/`
- Compose files stored in `~/.homelab/stacks/{name}.yml` (git-tracked)
- When deploying, injects Traefik labels + network config if `TraefikHost` is set
- Auto-registers DNS entries for `TraefikHost` in the appropriate zone

**API endpoints**
```
GET    /api/catalog                    # List all custom stacks
POST   /api/catalog                    # Add a custom stack
DELETE /api/catalog/{name}             # Remove
PUT    /api/catalog/{name}/enable      # Enable
PUT    /api/catalog/{name}/disable     # Disable
POST   /api/catalog/{name}/deploy      # Deploy
POST   /api/catalog/{name}/teardown    # Teardown
```

**CLI commands**
```
homelab catalog list
homelab catalog add <name> --compose <path> [--host <traefik-host>]
homelab catalog remove <name>
homelab catalog deploy <name>
homelab catalog teardown <name>
```

**Deploy integration**: The orchestrator deploys custom stacks after built-in stacks, respecting `depends-on` ordering. Custom stacks participate in `homelab status`, `homelab health`, and `homelab teardown` like built-in ones.

---

### 10.2 Backup Strategy

Automated backup for all stateful services with retention, scheduling, and restore-to-point-in-time.

**Domain (`HomeLab`)**

```csharp
public record BackupDefinition
{
    public required string StackName { get; init; }       // "core", "storage", "identity", or custom
    public required string Strategy { get; init; }        // "volume-tar", "command", "restic"
    public string? Command { get; init; }                 // e.g. "gitlab-backup create" (for "command" strategy)
    public IReadOnlyList<string> Volumes { get; init; } = []; // for "volume-tar" strategy
    public string Schedule { get; init; } = "0 3 * * *";  // cron expression (default: 3 AM daily)
    public int RetentionDays { get; init; } = 30;
    public string? DestinationBucket { get; init; }       // MinIO bucket for offsite copy
}

public record BackupEntry
{
    public required string Id { get; init; }              // timestamp-based
    public required string StackName { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required long SizeBytes { get; init; }
    public required string Location { get; init; }        // local path or S3 URI
}

public interface IBackupManager
{
    Task<Result<BackupEntry>> BackupAsync(string stackName, CancellationToken ct);
    Task<Result<Unit>> RestoreAsync(string stackName, string backupId, CancellationToken ct);
    Task<Result<IReadOnlyList<BackupEntry>>> ListAsync(string? stackName, CancellationToken ct);
    Task<Result<Unit>> PruneAsync(string stackName, CancellationToken ct); // Apply retention
}

public interface IBackupScheduler
{
    Task<Result<Unit>> StartAsync(IReadOnlyList<BackupDefinition> definitions, CancellationToken ct);
    Task<Result<Unit>> StopAsync(CancellationToken ct);
}
```

**Config**
```yaml
backups:
  destination: ~/.homelab/backups       # Local backup root
  offsite-bucket: homelab-backups       # MinIO bucket for copies (optional)

  schedules:
    - stack: core
      strategy: command
      command: "gitlab-backup create CRON=1"
      schedule: "0 3 * * *"
      retention-days: 30

    - stack: identity
      strategy: volume-tar
      volumes: [keycloak-postgres-data]
      schedule: "0 4 * * *"
      retention-days: 14

    - stack: storage
      strategy: volume-tar
      volumes: [minio-data]
      schedule: "0 2 * * 0"           # Weekly
      retention-days: 90
```

**Infrastructure** — `HomeLab.Infrastructure.Backup`
- `VolumeBackupManager.cs` — `podman volume export` → tar.gz → local path + optional MinIO upload
- `CommandBackupManager.cs` — `podman exec` into container to run backup command (e.g., `gitlab-backup create`)
- `BackupScheduler.cs` — cron-based timer (runs inside the daemon), triggers `IBackupManager.BackupAsync`
- `BackupPruner.cs` — deletes backups older than `RetentionDays`

**API endpoints**
```
GET    /api/backups                       # List all backups (filterable by stack)
POST   /api/backups/{stack}               # Trigger immediate backup
POST   /api/backups/{stack}/restore/{id}  # Restore from backup
DELETE /api/backups/{stack}/prune          # Apply retention policy
GET    /api/backups/schedule              # Show backup schedule
```

**CLI commands**
```
homelab backups list [--stack <name>]
homelab backups create <stack>
homelab backups restore <stack> <backup-id>
homelab backups prune [--stack <name>]
homelab backups schedule                   # Show schedule
```

---

### 10.3 Health Monitoring + Notifications

Continuous health monitoring by the daemon with alerting via multiple channels.

**Domain (`HomeLab`)**

```csharp
public enum HealthStatus { Healthy, Degraded, Unhealthy, Unknown }

public record ServiceHealth
{
    public required string StackName { get; init; }
    public required string ServiceName { get; init; }
    public required HealthStatus Status { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset CheckedAt { get; init; }
    public TimeSpan ResponseTime { get; init; }
}

public record CertHealth
{
    public required string CertName { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required int DaysRemaining { get; init; }
    public required HealthStatus Status { get; init; }   // Healthy > 30d, Degraded 7-30d, Unhealthy < 7d
}

public interface IHealthMonitor
{
    Task<Result<IReadOnlyList<ServiceHealth>>> CheckAllAsync(CancellationToken ct);
    Task<Result<ServiceHealth>> CheckAsync(string stackName, string serviceName, CancellationToken ct);
    Task<Result<IReadOnlyList<CertHealth>>> CheckCertsAsync(CancellationToken ct);
}

public interface INotificationChannel
{
    string ChannelType { get; }       // "gotify", "email", "slack", "webhook"
    Task<Result<Unit>> SendAsync(Notification notification, CancellationToken ct);
}

public record Notification
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required NotificationSeverity Severity { get; init; }  // Info, Warning, Critical
}

public interface IHealthWatcher
{
    /// Runs in the daemon, polls on interval, fires notifications on state changes
    Task<Result<Unit>> StartAsync(CancellationToken ct);
    Task<Result<Unit>> StopAsync(CancellationToken ct);
}
```

**Config**
```yaml
monitoring:
  enabled: true
  interval-seconds: 60                 # Health check polling interval
  cert-warning-days: [30, 7, 1]        # Alert thresholds for cert expiry

  notifications:
    - channel: gotify
      url: https://gotify.homelab.local
      token-secret: gotify-token
      severity: [warning, critical]    # Only send for these levels

    - channel: slack
      webhook-secret: slack-webhook-url
      severity: [critical]

    - channel: email
      smtp-host: smtp.homelab.local
      smtp-port: 587
      from: homelab@homelab.local
      to: [admin@homelab.local]
      severity: [critical]
```

**Health check methods** (per service):
- **GitLab**: `GET /-/readiness` → 200
- **Keycloak**: `GET /health/ready` → 200
- **Traefik**: `GET /ping` → 200
- **MinIO**: `GET /minio/health/live` → 200
- **Keycloak-PG**: `podman exec keycloak-postgres pg_isready`
- **Runners**: `podman exec gitlab-runner-{name} gitlab-runner verify`
- **Custom stacks**: configurable health URL or TCP port check

**Infrastructure** — `HomeLab.Infrastructure.Monitoring`
- `HttpHealthChecker.cs` — GET health URLs, parse response, measure latency
- `GotifyNotificationChannel.cs` — POST to Gotify API
- `SlackNotificationChannel.cs` — POST to Slack webhook
- `EmailNotificationChannel.cs` — SMTP send
- `WebhookNotificationChannel.cs` — generic POST to URL
- `HealthWatcher.cs` — `Timer`-based loop, state diffing (only alerts on transitions)

**API endpoints**
```
GET    /api/health                        # Full health report
GET    /api/health/{stack}                # Health for one stack
GET    /api/health/certs                  # Certificate health
GET    /api/health/history                # Health state history (last N checks)
PUT    /api/monitoring/enable             # Enable/disable monitoring
PUT    /api/monitoring/disable
```

**CLI commands**
```
homelab health                             # Already in core plan — enhanced with status colors
homelab health --watch                     # Live-updating health display
homelab health certs                       # Certificate expiry report
```

---

### 10.4 Resource Monitoring

Real-time resource usage per machine and per container, with Prometheus-compatible metrics export.

**Domain (`HomeLab`)**

```csharp
public record MachineResources
{
    public required string MachineName { get; init; }
    public required double CpuPercent { get; init; }
    public required long MemoryUsedBytes { get; init; }
    public required long MemoryTotalBytes { get; init; }
    public required long DiskUsedBytes { get; init; }
    public required long DiskTotalBytes { get; init; }
    public required DateTimeOffset CollectedAt { get; init; }
}

public record ContainerResources
{
    public required string MachineName { get; init; }
    public required string ContainerName { get; init; }
    public required string StackName { get; init; }
    public required double CpuPercent { get; init; }
    public required long MemoryUsedBytes { get; init; }
    public required long MemoryLimitBytes { get; init; }
    public required long NetInputBytes { get; init; }
    public required long NetOutputBytes { get; init; }
    public required long BlockInputBytes { get; init; }
    public required long BlockOutputBytes { get; init; }
    public required DateTimeOffset CollectedAt { get; init; }
}

public interface IResourceCollector
{
    Task<Result<IReadOnlyList<MachineResources>>> CollectMachineResourcesAsync(CancellationToken ct);
    Task<Result<IReadOnlyList<ContainerResources>>> CollectContainerResourcesAsync(string machineName, CancellationToken ct);
}

public interface IMetricsExporter
{
    /// Returns Prometheus text exposition format
    Task<Result<string>> ExportAsync(CancellationToken ct);
}
```

**Config**
```yaml
resources:
  enabled: true
  collection-interval-seconds: 30
  retention-hours: 24                    # In-memory ring buffer
  prometheus-enabled: true               # Expose /metrics endpoint
```

**Infrastructure** — `HomeLab.Infrastructure.Monitoring` (same project as health)
- `PodmanResourceCollector.cs` — `podman stats --format json --no-stream` per machine → parse
- `PodmanMachineResourceCollector.cs` — `podman machine inspect` → CPU/mem/disk
- `PrometheusMetricsExporter.cs` — formats metrics as Prometheus text exposition
- `ResourceRingBuffer.cs` — in-memory circular buffer for recent data points

**API endpoints**
```
GET    /api/resources                     # Current usage (all machines + containers)
GET    /api/resources/{machine}           # Per-machine detail
GET    /api/resources/history             # Recent data points from ring buffer
GET    /metrics                           # Prometheus scrape endpoint
```

**Grafana integration**: GitLab Omnibus includes bundled Prometheus + Grafana. The daemon's `/metrics` endpoint can be added as a Prometheus scrape target, giving a unified Grafana dashboard for both GitLab internals and HomeLab platform metrics.

**CLI commands**
```
homelab resources                          # Table: machine/container CPU/RAM/disk
homelab resources --watch                  # Live-updating (like htop)
homelab resources <machine>                # Per-machine detail
```

---

### 10.5 Update Management

Track container image versions, detect available updates, and perform rolling upgrades with automatic rollback.

**Domain (`HomeLab`)**

```csharp
public record ImageVersionInfo
{
    public required string Image { get; init; }           // "gitlab/gitlab-ce"
    public required string CurrentTag { get; init; }      // "17.8.0-ce.0"
    public string? LatestTag { get; init; }               // "17.9.1-ce.0" (null if unknown)
    public bool UpdateAvailable { get; init; }
    public DateTimeOffset? CheckedAt { get; init; }
}

public enum UpdateStrategy { RollingRestart, StopStart, BlueGreen }

public record UpdatePlan
{
    public required string StackName { get; init; }
    public required string ServiceName { get; init; }
    public required string FromTag { get; init; }
    public required string ToTag { get; init; }
    public required UpdateStrategy Strategy { get; init; }
    public bool AutoRollback { get; init; } = true;       // Rollback if health check fails after update
}

public interface IUpdateManager
{
    /// Check for available updates across all services
    Task<Result<IReadOnlyList<ImageVersionInfo>>> CheckUpdatesAsync(CancellationToken ct);

    /// Plan an update for a specific service
    Task<Result<UpdatePlan>> PlanUpdateAsync(string stackName, string? toTag, CancellationToken ct);

    /// Execute update: pull → recreate → health check → rollback on failure
    Task<Result<Unit>> ApplyUpdateAsync(UpdatePlan plan, CancellationToken ct);

    /// Rollback to previous image tag
    Task<Result<Unit>> RollbackAsync(string stackName, CancellationToken ct);
}
```

**Config**
```yaml
updates:
  auto-check: true
  check-interval-hours: 24
  auto-apply: false                      # Manual approval required
  default-strategy: rolling-restart
  notifications: true                    # Notify on available updates

  pinned:                                # Lock specific images to a tag
    - image: gitlab/gitlab-ce
      tag: "17.8.0-ce.0"                # Don't auto-update GitLab (manual upgrades)
    - image: postgres
      tag: "17-alpine"                   # Pin Postgres major version
```

**Infrastructure** — `HomeLab.Infrastructure.Updates`
- `RegistryVersionChecker.cs` — queries Docker Hub / Quay.io registry API for latest tags
- `PodmanUpdateExecutor.cs` — `podman pull` → `podman-compose up -d` (recreates with new image)
- `RollbackTracker.cs` — stores previous image digest before update, restores on failure
- Health check integration: after update, waits for service health → rolls back if unhealthy

**Update flow**:
1. `CheckUpdatesAsync` → compares current running image digest vs registry latest
2. User reviews: `homelab updates list` shows available updates
3. User approves: `homelab updates apply core --to 17.9.1-ce.0`
4. System: pulls image → stops service → starts with new image → health check
5. If health fails within 60s: auto-rollback to previous image + notify

**API endpoints**
```
GET    /api/updates                       # List available updates
GET    /api/updates/{stack}               # Updates for one stack
POST   /api/updates/{stack}/apply         # Apply update (optional: ?tag=x.y.z)
POST   /api/updates/{stack}/rollback      # Rollback to previous
GET    /api/updates/history               # Update history (who, when, what, success/rollback)
```

**CLI commands**
```
homelab updates list                       # Show available updates
homelab updates check                      # Force check now
homelab updates apply <stack> [--to <tag>] # Apply update
homelab updates rollback <stack>           # Rollback
homelab updates history                    # Update log
homelab updates pin <image> <tag>          # Pin an image version
```

---

### 10.6 Dashboard

Blazor Server SPA served by the daemon, consuming the same `/api/*` endpoints. Full management UI.

**Project** — `FrenchExDev.Net.HomeLab.Dashboard` (Blazor Server, served by `HomeLab.Api`)

```
HomeLab/
├── src/
│   └── FrenchExDev.Net.HomeLab.Dashboard/    # Blazor Server components
```

```xml
<!-- HomeLab.Dashboard.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <ProjectReference Include="../FrenchExDev.Net.HomeLab/..." />   <!-- Domain models for display -->
</Project>

<!-- HomeLab.Api.csproj adds: -->
<ProjectReference Include="../FrenchExDev.Net.HomeLab.Dashboard/..." />
```

**Pages / Components**

| Page | Route | Description |
|------|-------|-------------|
| **Overview** | `/` | Status cards: machines (running/stopped), stacks (healthy/unhealthy), cert expiry, DNS zones, resource usage gauges |
| **Machines** | `/machines` | List + add/edit/remove. Machine cards with CPU/RAM/disk gauges. Start/stop buttons. |
| **Stacks** | `/stacks` | Built-in + custom stacks. Deploy/teardown toggles. Log viewer. |
| **DNS** | `/dns` | Zones table. Add/remove entries. Zone reconciliation status. |
| **PKI** | `/pki` | Cert tree (CA → service certs). Expiry timeline. Trust/untrust buttons. Renew action. |
| **Runners** | `/runners` | Runner cards with status/tags. Register/unregister. Job count. |
| **Catalog** | `/catalog` | Add custom services via form (upload compose file). Enable/disable toggles. |
| **Backups** | `/backups` | Backup history table. Manual trigger button. Restore wizard. Schedule view. |
| **Updates** | `/updates` | Available updates table. Apply/rollback buttons. Pin management. History. |
| **Resources** | `/resources` | Live CPU/RAM/disk charts per machine. Container breakdown table. |
| **Config** | `/config` | YAML editor (with validation). Git status bar. Commit/push/pull buttons. Diff viewer. |
| **Settings** | `/settings` | Edit `homelab.yml` sections via forms. Notification channel configuration. |

**Real-time updates**:
- Blazor Server uses SignalR (built-in) for real-time push
- Health status changes → instant UI update (no polling)
- Resource metrics → live chart updates every 30s
- Log streaming → real-time log tail in stack detail view

**Auth integration**:
- Keycloak OIDC login (same as API auth)
- Role-based UI: admin sees all, viewer sees read-only
- Bootstrap mode: API key auth before Keycloak is deployed

**Served by the daemon**:
```csharp
// In HomeLab.Api Program.cs
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
// Dashboard is served at the same :9443 as the API
// API at /api/*, Dashboard at /*
```

---

## 11. Testing

### Fakes (`HomeLab.Testing`)

```
├── FakeMachineProvider.cs          # In-memory machine state
├── FakeMachineReconciler.cs
├── FakeDnsResolver.cs
├── FakeCertificateAuthority.cs
├── FakeTrustStoreManager.cs
├── FakeStackDeployer.cs
├── FakeRunnerRegistrar.cs
├── FakeSecretsManager.cs
├── FakeConfigRepository.cs
└── Builders/
    ├── HomeLabConfigBuilder.cs
    └── MachineDefinitionBuilder.cs
```

### Test Coverage

- **Domain tests**: Orchestrator, DnsZoneReconciler, validators, stack builders, OmnibusConfigBuilder, multi-runner CiStack, config round-trip
- **Infrastructure slice tests** (each slice independently): `.Pki` (real cert gen, trust store), `.FileSystem` (hosts file, secrets), `.Git` (init/commit), `.Podman` (mocked output), `.PodmanCompose` (deploy, YAML), `.PiHole` (per-record URL encoding), `.Ssh` (connectivity)
- **Lib tests**: DI wiring resolves correctly, use cases delegate to correct interfaces
- **API tests**: Endpoint routing, request/response serialization (using WebApplicationFactory)

---

## 12. Full Configuration Model

```yaml
domain: homelab.local

homelab-dir: ~/.homelab              # Git repo + data root

machines:
  - name: homelab
    provider: podman
    cpus: 4
    memory: 8192
    disk-size: 100
    rootful: true
    purpose: Service host

  - name: homelab-ci
    provider: podman
    cpus: 4
    memory: 4096
    disk-size: 50
    rootful: true
    purpose: CI runners

  - name: remote-build
    provider: ssh
    host: 192.168.1.50
    user: build
    ssh-key-secret: remote-build-ssh-key
    purpose: Remote build machine

pki:
  ca-common-name: HomeLab Root CA
  ca-validity-years: 10
  cert-validity-days: 365
  key-algorithm: rsa-4096
  sans:
    - "*.homelab.local"
    - "*.v1.homelab.local"
    - "homelab.local"
    - "127.0.0.1"

dns:
  provider: pihole
  pihole:
    base-url: https://192.168.1.2
    password-secret: pihole-password
  zones:
    - suffix: homelab.local
      target-ip: 192.168.1.100
      hostnames: [gitlab, auth, registry, minio, minio-console, traefik, api]
    - suffix: v1.homelab.local
      target-ip: 192.168.1.101
      hostnames: [gitlab, auth, registry, minio, traefik]

gitlab:
  image: gitlab/gitlab-ce:17.8.0-ce.0
  ssh-port: 2222
  registry-enabled: true
  machine: homelab

keycloak:
  image: quay.io/keycloak/keycloak:26.1
  admin-user: admin
  realm: homelab
  postgres-image: postgres:17-alpine
  machine: homelab

runners:
  - name: shell-runner
    machine: homelab-ci
    executor: shell
    tags: [shell, linux]
  - name: docker-runner
    machine: homelab-ci
    executor: docker
    tags: [docker, build]
    limit: 4
  - name: heavy-runner
    machine: remote-build
    executor: docker
    tags: [heavy, long-running]
    limit: 2

storage:
  enabled: true
  minio-image: minio/minio:latest
  machine: homelab

api:
  port: 9443
  auto-commit: true               # Auto-commit config changes
  auto-push: false                 # Manual push to GitLab

compose-output: generated          # Relative to homelab-dir
```

---

## 13. Implementation Sequence

### Phase 1 — IHttpClient augmentation
### Phase 2 — PiHole solution (client + testing + tests)
### Phase 3 — HomeLab scaffolding (35 projects + .slnx + quality-gate + master .slnx)

### Phase 4a — Domain.Messaging contracts
- `IDomainEvent : INotification`, `DomainEvent` base record
- `ICommand<T>`, `ICommandHandler<T,R>`, `IQuery<T>`, `IQueryHandler<T,R>`
- `IDomainEventBus`
- Tests: contract interface constraints, base record defaults

### Phase 4b — Shared Kernel (`HomeLab`)
- Config model + all sub-configs + `HomeLabConfig.Load()/Default()`
- All domain interfaces (IMachineProvider, IDnsResolver, ICertificateAuthority, etc.)
- All 82 domain events (records inheriting `DomainEvent`)
- All commands + queries (records implementing `ICommand<T>` / `IQuery<T>`)
- All value objects (MachineState, DnsEntry, CaBundle, ReconciliationPlan, etc.)
- Constants (HomeLabNetworks, HomeLabVolumes)
- Tests: config round-trip, event instantiation

### Phase 4c — Infrastructure.Messaging
- MediatR registration (`AddHomeLabMessaging()`)
- Pipeline behaviors: `LoggingBehavior`, `ValidationBehavior`, `TimingBehavior`
- `MediatRDomainEventBus` adapter
- Tests: behavior ordering, event dispatch, pipeline short-circuit on validation failure

### Phase 5 — Domain: `HomeLab.Domain.Dns`
- `DnsZoneReconciler` (pure: desired zones + current → add/remove actions)
- Tests: multi-zone coexistence, suffix filtering, idempotency

### Phase 6 — Domain: `HomeLab.Domain.Machines`
- `MachineReconciler` (routes to correct IMachineProvider, diffs desired vs actual)
- Tests: create/update/start/skip, never touches unmanaged, multi-provider

### Phase 7 — Domain: `HomeLab.Domain.Stacks`
- `IngressStack`, `IdentityStack`, `StorageStack`, `CoreStack` + `OmnibusConfigBuilder`, `CiStack` (multi-runner)
- Tests: each stack's ComposeFile output, Omnibus Ruby string, multi-runner services

### Phase 8 — Domain: `HomeLab.Domain.Validation`
- `HomeLabValidator` (cross-cutting: ports, machine refs, zones, secrets, OIDC)
- Tests: all validation rules

### Phase 9 — Domain: `HomeLab.Domain.Operations`
- `HomeLabOrchestrator` + `DeploymentPlanBuilder`
- Tests via fakes: deploy order, teardown reverse, error propagation via Bind

### Phase 10 — Testing library (fakes for all shared kernel interfaces + test data builders)

### Phase 11 — Infrastructure slices (one at a time, each with tests)
- 11a: `.Infrastructure.Messaging` already done in Phase 4c
- 11b: `.Infrastructure.Pki` (X509 — self-contained)
- 11c: `.Infrastructure.FileSystem` (hosts file + secrets + audit log)
- 11d: `.Infrastructure.Git` (git CLI)
- 11e: `.Infrastructure.Podman` (PodmanMachineProvider)
- 11f: `.Infrastructure.PodmanCompose` (deployer + registrar + YAML)
- 11g: `.Infrastructure.PiHole` (PiHoleDnsResolver)
- 11h: `.Infrastructure.Ssh` (SshMachineProvider)

### Phase 12 — Lib (DI registration `AddHomeLab()` + all use cases)
### Phase 13 — CLI (full command tree)
### Phase 14 — API (endpoints + daemon service registration)
### Phase 15 — Quality gates (PiHole + HomeLab)

---

## 14. Cross-Cutting Concerns

### 14.1 Bootstrap — Complete How-To

#### Prerequisites (what you need before `homelab init`)

| Requirement | Why | How to check |
|-------------|-----|-------------|
| **Podman** installed | Machine provider | `podman --version` (≥ 5.0) |
| **Podman machine** capability | VM backend | `podman machine info` |
| **Pi-hole** running on LAN | DNS provider | `curl http://192.168.1.2/admin/` |
| **git** installed | Config versioning | `git --version` |
| **age** installed | Secret encryption | `age --version` |
| **sops** installed | Structured encryption | `sops --version` |
| **.NET 10 SDK** | Build + run | `dotnet --version` |
| **Ports 80, 443, 2222, 9443 free** | Services + API | `netstat -tlnp` |

#### Step 0: Install the CLI

```bash
dotnet tool install --global FrenchExDev.Net.HomeLab.Cli
# or from local build:
dotnet run --project HomeLab/src/FrenchExDev.Net.HomeLab.Cli -- --help
```

#### Step 1: Initialize (`homelab init`)

```bash
homelab init
# Creates:
#   ~/.homelab/                     ← git repo root
#   ~/.homelab/homelab.yml          ← default config (edit this!)
#   ~/.homelab/.gitignore           ← ignores generated/, .sops-age-key, .lock
#   ~/.homelab/stacks/              ← custom stack compose files
#   ~/.homelab/secrets/             ← will hold encrypted .env files
#   git init + initial commit: "homelab: init"
#
# Emits events: ConfigLoaded, BootstrapApiKeyGenerated, ConfigCommitted
```

**Edit `~/.homelab/homelab.yml`** — configure your domain, Pi-hole IP, machine specs, runner definitions. The defaults are sensible but you'll want to set:
- `domain:` your chosen domain (e.g., `homelab.local`)
- `dns.pihole.base-url:` your Pi-hole's address
- `machines:` CPU/memory for your hardware
- `pki.sans:` must include `*.{domain}`

#### Step 2: Validate config

```bash
homelab validate
# Checks: port conflicts, machine refs, zone suffixes, secret requirements, OIDC consistency
# Fix any errors before proceeding
# Emits: ConfigValidated or ConfigValidationFailed
```

#### Step 3: Generate secrets (`homelab secrets init`)

```bash
homelab secrets init
# Generates random secrets for:
#   - API key (for bootstrap auth)
#   - GitLab root password
#   - Keycloak admin password
#   - Keycloak PostgreSQL password
#   - MinIO root user + root password
#   - Runner registration token placeholder
# Writes to ~/.homelab/secrets/*.env
# Generates age keypair → ~/.homelab/.sops-age-key (gitignored!)
# Encrypts all .env files via SOPS
# Commits: "homelab: generate secrets"
# Emits: SecretsGenerated, SecretsEncrypted, ConfigCommitted
#
# ⚠️  BACK UP ~/.homelab/.sops-age-key — losing it means losing access to secrets!
```

#### Step 4: Create Podman machines (`homelab machines apply`)

```bash
homelab machines plan            # Dry-run: shows what will be created
homelab machines apply           # Creates + starts machines
# For 2 machines (homelab + homelab-ci):
#   podman machine init homelab --cpus 4 --memory 8192 --disk-size 100 --rootful
#   podman machine start homelab
#   podman machine init homelab-ci --cpus 4 --memory 4096 --disk-size 50 --rootful
#   podman machine start homelab-ci
#
# ⏱ Takes: 2-5 minutes (image download on first run)
# Emits: MachineCreated × 2, MachineStarted × 2, MachineReconciliationApplied
```

**Verify**: `podman machine list` shows both machines running.

#### Step 5: Generate certificates (`homelab pki init`)

```bash
homelab pki init
# Generates:
#   ~/.homelab/pki/ca.pem + ca-key.pem          ← Root CA (RSA-4096, 10yr)
#   ~/.homelab/pki/wildcard.pem + wildcard-key.pem  ← Service cert (SANs: *.homelab.local, etc.)
# Copies PEM files into podman machine volumes:
#   podman machine ssh homelab -- mkdir -p /mnt/homelab-data/pki
#   podman machine cp ~/.homelab/pki/ homelab:/mnt/homelab-data/pki/
# Commits: "homelab: generate pki"
# Emits: RootCaGenerated, ServiceCertIssued, CertCopiedToMachine, ConfigCommitted
```

#### Step 6: Trust the root CA (`homelab pki trust`)

```bash
homelab pki trust
# Windows: Adds CA to CurrentUser\Root cert store
# Linux: Copies to /usr/local/share/ca-certificates/ + update-ca-certificates
#
# Also copies CA into CI machine for runner trust:
#   podman machine cp ~/.homelab/pki/ca.pem homelab-ci:/etc/pki/ca-trust/source/anchors/
#   podman machine ssh homelab-ci -- update-ca-trust
#
# Emits: RootCaTrusted, CertCopiedToMachine
```

**Verify**: `curl --cacert ~/.homelab/pki/ca.pem https://localhost` (will fail with connection refused — that's fine, cert is valid).

#### Step 7: Register DNS (`homelab dns apply`)

```bash
homelab dns apply
# For each zone × hostname, calls Pi-hole API:
#   PUT /api/config/dns/hosts/192.168.1.100%20gitlab.homelab.local
#   PUT /api/config/dns/hosts/192.168.1.100%20auth.homelab.local
#   PUT /api/config/dns/hosts/192.168.1.100%20registry.homelab.local
#   PUT /api/config/dns/hosts/192.168.1.100%20minio.homelab.local
#   PUT /api/config/dns/hosts/192.168.1.100%20minio-console.homelab.local
#   PUT /api/config/dns/hosts/192.168.1.100%20traefik.homelab.local
#   PUT /api/config/dns/hosts/192.168.1.100%20api.homelab.local
#   (repeat for .v1.homelab.local zone if configured)
#
# Emits: DnsRecordAdded × N, DnsZoneReconciled
```

**Verify**: `nslookup gitlab.homelab.local 192.168.1.2` resolves to your machine IP.

#### Step 8: Deploy Ingress — Traefik

```bash
homelab stacks deploy ingress
# Generates ~/.homelab/generated/ingress.yml
# Deploys via podman-compose on "homelab" machine:
#   CONTAINER_HOST=... podman-compose -f ingress.yml up -d
#
# Traefik reads wildcard cert from volume mount
# Ports: 80 (→443 redirect), 443, 8080 (dashboard)
#
# ⏱ Takes: 10-30 seconds
# Emits: ComposeFileWritten, StackDeployed, PortBindingAllocated × 3
```

**Verify**: `curl -k https://traefik.homelab.local/ping` → 200 OK.

#### Step 9: Deploy Identity — Keycloak + PostgreSQL

```bash
homelab stacks deploy identity
# Starts Keycloak PostgreSQL first (healthcheck: pg_isready)
# Then starts Keycloak (waits for PG healthy)
#
# Keycloak auto-imports realm on first boot (if realm-export.json provided)
# Otherwise, initial setup needed (see below)
#
# ⏱ Takes: 1-3 minutes (Keycloak first boot is slow)
# Emits: StackDeployed
```

**Verify**: `curl -k https://auth.homelab.local/health/ready` → 200.

**Keycloak initial configuration** (automated via realm import or manual):

The stack generates a `realm-export.json` with:
- Realm: `homelab`
- Clients pre-configured:
  - `gitlab` — OIDC client for GitLab (`redirect_uri: https://gitlab.homelab.local/users/auth/openid_connect/callback`)
  - `minio` — OIDC client for MinIO console
  - `homelab-api` — OIDC client for the daemon API
  - `traefik-forward-auth` — for dashboard protection
- Admin user created from `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD` secrets

Keycloak mounts this file and imports on first start via `--import-realm`.

#### Step 10: Deploy Storage — MinIO

```bash
homelab stacks deploy storage
# Starts MinIO with MINIO_ROOT_USER + MINIO_ROOT_PASSWORD from secrets
# Auto-creates buckets via mc (MinIO client) init container:
#   mc alias set local http://minio:9000 $ROOT_USER $ROOT_PASSWORD
#   mc mb local/gitlab-artifacts
#   mc mb local/gitlab-lfs
#   mc mb local/gitlab-uploads
#   mc mb local/gitlab-backups
#
# ⏱ Takes: 10-30 seconds
# Emits: StackDeployed
```

**Verify**: `curl -k https://minio-console.homelab.local` → MinIO Console login page.

#### Step 11: Deploy Core — GitLab Omnibus

```bash
homelab stacks deploy core
# This is the big one. GitLab Omnibus container with:
#   - GITLAB_OMNIBUS_CONFIG pointing to Keycloak OIDC, MinIO object_store
#   - SSL certs from PKI volume mount
#   - Container Registry enabled
#   - Bundled Prometheus + Grafana
#
# ⏱ Takes: 3-8 minutes (GitLab first reconfigure is SLOW)
#   The CLI shows a progress indicator + health polling:
#   "Waiting for GitLab... [2m 15s] reconfiguring..."
#   "Waiting for GitLab... [4m 30s] starting services..."
#   "GitLab is healthy ✓"
#
# Emits: StackDeployed
```

**Health wait**: The orchestrator polls `GET https://gitlab.homelab.local/-/readiness` every 10 seconds, up to 10 minutes. If GitLab doesn't become healthy, the deploy reports failure but doesn't roll back (GitLab may just need more time).

**Verify**: `curl -k https://gitlab.homelab.local/-/readiness` → 200.

**First login**: Navigate to `https://gitlab.homelab.local`. Login with:
- Username: `root`
- Password: from `~/.homelab/secrets/gitlab.env` (`GITLAB_ROOT_PASSWORD`)
- Or click "Sign in with Keycloak" if OIDC is configured

#### Step 12: Start the daemon (`homelab daemon start`)

```bash
homelab daemon start
# Starts the API daemon on :9443
# Initially uses API key auth (bootstrap mode)
# Probes Keycloak: if healthy → switches to composite auth (OIDC + API key fallback)
#
# Emits: DaemonStarted, AuthModeChanged (apikey → composite)
```

**Verify**: `curl -k -H "Authorization: Bearer $(cat ~/.homelab/secrets/api-key)" https://api.homelab.local:9443/api/status`

#### Step 13: Register runners

```bash
# Get registration token from GitLab:
#   GitLab Admin → CI/CD → Runners → "New instance runner" → copy token
#
homelab runners register-all --token glrt-XXXXXXXXXXXXXXXXXXXX
# For each runner in config:
#   podman machine ssh homelab-ci -- podman exec gitlab-runner-shell \
#     gitlab-runner register \
#       --non-interactive \
#       --url https://gitlab.homelab.local \
#       --token glrt-XXX \
#       --executor shell \
#       --tag-list "shell,linux"
#
# Emits: RunnerRegistered × N
```

**Verify**: GitLab Admin → CI/CD → Runners → all runners show "online" (green).

#### Step 14: Push config to GitLab (self-referential!)

```bash
# Create the config repo in GitLab (manually or via API):
#   GitLab → New Project → "homelab-config" (private)

homelab config set-remote git@gitlab.homelab.local:root/homelab-config.git
homelab config push
# Pushes all config (homelab.yml, encrypted secrets, pki certs, stacks/) to GitLab
#
# Emits: ConfigRemoteSet, ConfigPushed
```

**The homelab is now self-hosting its own configuration.**

#### Resumability — Idempotent Re-run

Every step is idempotent. If bootstrap fails at step 11 (GitLab), you can re-run:

```bash
homelab deploy          # Runs the full pipeline; skips already-done steps:
                        # - Secrets: exist → skip
                        # - Machines: running → skip
                        # - PKI: CA exists → load (not regenerate)
                        # - Trust: already trusted → skip
                        # - DNS: records exist → skip (reconcile adds missing only)
                        # - Stacks: running + healthy → skip; unhealthy → recreate
                        # - Runners: registered → skip
```

Or re-run a single step: `homelab stacks deploy core` (only redeploys GitLab).

#### Timing Summary

| Step | Duration | Notes |
|------|----------|-------|
| `init` | < 1s | Scaffolding only |
| `secrets init` | < 1s | Random generation + SOPS |
| `machines apply` | 2-5 min | Image download on first run |
| `pki init` | < 2s | RSA key generation |
| `pki trust` | < 1s | Cert store write |
| `dns apply` | 1-5s | Pi-hole API calls |
| Ingress (Traefik) | 10-30s | Fast |
| Identity (Keycloak) | 1-3 min | DB init + realm import |
| Storage (MinIO) | 10-30s | Fast + bucket creation |
| Core (GitLab) | **3-8 min** | The bottleneck (reconfigure) |
| Runners | 5-15s | Registration API calls |
| **Total first deploy** | **~10-18 min** | Subsequent deploys: < 1 min (all cached) |

#### Error Recovery

| Failure | What happens | Recovery |
|---------|-------------|----------|
| Machine creation fails | Error reported, no cleanup | Fix issue, re-run `homelab machines apply` |
| PKI generation fails | No certs on disk | Re-run `homelab pki init` |
| DNS fails (Pi-hole unreachable) | Partial records | Fix Pi-hole, re-run `homelab dns apply` (idempotent) |
| Traefik fails to start | Container in error state | Check logs: `homelab stacks logs ingress`. Fix config, redeploy. |
| Keycloak fails | Usually PG connection | Check `homelab stacks logs identity`. Verify PG is healthy first. |
| GitLab times out | Still reconfiguring | Wait and re-check: `homelab health`. GitLab can take 10+ min on low-spec machines. |
| Runner registration fails | Token invalid or GitLab unreachable | Verify GitLab is healthy, get fresh token, re-run `homelab runners register-all --token NEW_TOKEN` |
| SOPS decryption fails | Missing age key | Copy `.sops-age-key` from backup. |

#### Auth Strategy — Two-Phase Detail

```csharp
public interface IAuthStrategy
{
    Task<Result<AuthPrincipal>> AuthenticateAsync(HttpContext context, CancellationToken ct);
}

public class BootstrapApiKeyAuth : IAuthStrategy
{
    // Checks Authorization: Bearer {key} against stored hash
    // Used during bootstrap before Keycloak exists
}

public class KeycloakOidcAuth : IAuthStrategy
{
    // Standard OIDC bearer token validation against Keycloak
    // Validates JWT: issuer, audience, expiry, signature
}

public class CompositeAuth : IAuthStrategy
{
    // Tries OIDC first → if Keycloak unreachable or token invalid, falls back to API key
    // This ensures the API never becomes inaccessible even if Keycloak is down
    // Emits AuthModeChanged on transitions
}
```

The daemon starts in **API key mode**. A background task probes `https://auth.{domain}/health/ready` every 30 seconds. Once Keycloak responds healthy, the daemon switches to **composite mode** and emits `AuthModeChanged`. If Keycloak later goes down, composite mode transparently falls back to API key (no restart needed).

#### Reproducing on a New Machine

```bash
# On the new machine (prerequisites installed):
# 1. Copy the age key (the ONLY manual step):
scp old-machine:~/.homelab/.sops-age-key ~/.homelab/.sops-age-key

# 2. Clone config from GitLab (or from any git remote):
git clone git@gitlab.homelab.local:root/homelab-config.git ~/.homelab

# 3. Deploy everything:
homelab deploy
# All 14 steps run automatically. Secrets are decrypted in-memory via SOPS.
# ~10-18 minutes later: full homelab running.
```

#### Aspire Dev Bootstrap (Mode A)

For development, the Aspire AppHost handles all of this automatically:

```bash
dotnet run --project HomeLab.AppHost
# Aspire spins up: Keycloak container, MinIO container, GitLab container, Traefik container
# Our API runs as a debuggable process
# No machines, no PKI, no DNS needed — Aspire manages ports + service discovery
# Dashboard at localhost:18888
```

---

### 14.2 Secrets Encryption at Rest

Secrets committed to the git-backed config **must be encrypted**. Using **age** (modern, simple, no GPG) + **SOPS** (structured encryption for YAML/JSON/env files).

**Domain (`HomeLab` shared kernel)**:
```csharp
public interface ISecretsEncryptor
{
    Task<Result<Unit>> EncryptFileAsync(string path, CancellationToken ct);
    Task<Result<Unit>> DecryptFileAsync(string path, CancellationToken ct);
    Task<Result<string>> DecryptValueAsync(string encryptedValue, CancellationToken ct);
}
```

**Infrastructure** — `HomeLab.Infrastructure.Secrets` (rename from `.FileSystem` secrets part):
- `SopsEncryptor.cs` — wraps `sops --encrypt` / `sops --decrypt` CLI
- Age key stored in `~/.homelab/.sops-age-key` (gitignored, never committed)
- `.sops.yaml` in repo root configures which files are encrypted:
  ```yaml
  creation_rules:
    - path_regex: secrets/.*\.env$
      age: "age1..."
  ```

**Workflow**:
1. `homelab secrets init` → generates age keypair + `.sops.yaml`, encrypts all `.env` files
2. Encrypted files are safe to commit/push to GitLab
3. `homelab deploy` → auto-decrypts in-memory, never writes plaintext to disk
4. New machine setup: copy `~/.homelab/.sops-age-key` (one manual step, then `git clone` + `homelab deploy` works)

**Config**:
```yaml
secrets:
  encryption: sops-age              # "sops-age", "sops-gpg", or "plaintext" (dev only)
  age-key-path: .sops-age-key      # Relative to homelab-dir (gitignored)
```

**New package needed**: None. SOPS and age are CLI tools invoked via `Process` (same as git). Add to `Directory.Packages.props`: nothing.

---

### 14.3 Podman Machine Port Forwarding

Containers inside a Podman machine need ports exposed to the host. On Windows/macOS, Podman machines run in a VM — port forwarding is required.

**How it works**:
- `podman machine` automatically forwards ports from rootful containers to the host when `--rootful` is set
- When a container binds `:443`, it becomes accessible on `localhost:443` on the host
- This is handled by gvproxy (the Podman machine networking daemon)

**What we need to plan**:
- All stacks must use explicit port bindings in compose files (e.g., `"443:443"`)
- Only Traefik exposes ports to the host (80, 443, 8080, 2222 for SSH)
- Other services are internal (accessible only via Traefik routing)
- The daemon API (`:9443`) runs on the **host** directly (not inside a machine), so no forwarding needed

**Port conflict detection** (in `HomeLabValidator`):
```csharp
// Validate no two stacks bind the same host port on the same machine
// Validate reserved ports: 9443 (daemon API) is never used by stacks
```

**Config for custom port mappings**:
```yaml
ports:
  http: 80
  https: 443
  ssh: 2222
  traefik-dashboard: 8080
  daemon-api: 9443
```

---

### 14.4 Podman Socket / Connection Discovery

Each Podman machine has its own socket. The `--connection` flag (or `CONTAINER_HOST` env var) targets a specific machine.

**Implementation in `HomeLab.Infrastructure.Podman`**:
```csharp
public class MachineContext
{
    public required string MachineName { get; init; }

    /// Discovers the connection URI for this machine
    /// On Windows: "ssh://user@localhost:PORT/run/podman/podman.sock"
    /// On Linux: "unix:///run/user/UID/podman/podman.sock" (default machine)
    public async Task<Result<string>> DiscoverConnectionUriAsync(CancellationToken ct)
    {
        // podman system connection list --format json → find by machine name
    }

    public BinaryBinding CreatePodmanBinding()
    {
        // Sets environment variable CONTAINER_HOST or uses --connection flag
    }

    public BinaryBinding CreatePodmanComposeBinding()
    {
        // podman-compose uses CONTAINER_HOST env var or DOCKER_HOST
    }
}
```

**Discovery flow**:
1. `podman system connection list --format json` → returns all registered connections
2. Each machine registers a connection named `{machine-name}` (rootful) and `{machine-name}-root`
3. `MachineContext` looks up the connection by name, extracts the URI
4. Sets `CONTAINER_HOST={uri}` when invoking podman/podman-compose commands

---

### 14.5 Concurrency / Locking

Multiple CLI invocations or API requests must not corrupt state.

**File-based lock** (simple, cross-process):
```csharp
public interface IHomeLabLock
{
    Task<Result<IDisposable>> AcquireAsync(string operationName, TimeSpan timeout, CancellationToken ct);
}
```

**Implementation** — `HomeLab.Infrastructure.FileSystem`:
- Lock file: `~/.homelab/.lock`
- Uses `FileStream` with `FileShare.None` (OS-enforced exclusive access)
- Timeout with retry: if lock held > timeout, return error with holder info
- Lock file contains: `{"operation": "deploy", "pid": 1234, "started": "2026-03-18T..."}`
- Stale lock detection: if PID no longer running, force-release

**Where locks are applied**:
- `DeployUseCase`, `TeardownUseCase` — exclusive (only one at a time)
- `StatusUseCase`, `HealthUseCase` — no lock (read-only)
- `MachineUseCases.Apply` — exclusive
- `DnsUseCases.Apply` — exclusive
- `PkiUseCases.Init` — exclusive
- Config mutations (API POST/PUT/DELETE) — exclusive
- `GenerateUseCase` — shared (multiple reads OK, blocks deploys)

---

### 14.6 Idempotency

Every operation is safe to run multiple times with the same result.

**Design principles**:
- `deploy` checks current state before acting (reconcile, not blindly create)
- `dns apply` compares desired records vs actual → only adds missing, removes extra
- `pki init` loads existing CA if present, only generates if missing
- `machines apply` uses reconciliation (create only if missing, update only if different)
- `secrets init` skips secrets that already exist
- Compose `up -d` is inherently idempotent (podman-compose recreates only changed services)

**Testing**: Every use case has a "run twice" test that asserts the second run is a no-op.

---

### 14.7 Platform Differences (Windows vs Linux)

| Concern | Windows | Linux |
|---------|---------|-------|
| Hosts file | `C:\Windows\System32\drivers\etc\hosts` | `/etc/hosts` |
| Cert store | `X509Store(StoreName.Root, StoreLocation.CurrentUser)` | `update-ca-certificates` (requires file copy to `/usr/local/share/ca-certificates/`) |
| Daemon service | Windows Service (`BackgroundService` + `UseWindowsService()`) | systemd unit (`UseSystemd()`) |
| Podman machine | WSL2-based VM (gvproxy for port forwarding) | QEMU-based VM (or native rootless) |
| Podman socket | `npipe:////./pipe/podman-machine-{name}` | `unix:///run/user/{uid}/podman/podman.sock` |
| File locking | `FileShare.None` works | `FileShare.None` works (via flock) |
| Line endings | CRLF in hosts file | LF |

**Implementation**:
```csharp
public static class Platform
{
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static string HostsFilePath => IsWindows
        ? @"C:\Windows\System32\drivers\etc\hosts"
        : "/etc/hosts";
}
```

Cert trust on Linux needs a different approach — `X509Store` doesn't work for system-wide trust. Instead:
```csharp
public class LinuxTrustStoreManager : ITrustStoreManager
{
    // Copies CA PEM to /usr/local/share/ca-certificates/homelab-ca.crt
    // Runs: update-ca-certificates (requires sudo)
}
```

The `ITrustStoreManager` has two infrastructure implementations:
- `HomeLab.Infrastructure.Pki/WindowsTrustStoreManager.cs`
- `HomeLab.Infrastructure.Pki/LinuxTrustStoreManager.cs`

`HomeLab.Lib` registers the correct one based on `RuntimeInformation`.

---

### 14.8 Logging Infrastructure

Structured logging throughout, with multiple sinks.

**Strategy**:
- `Microsoft.Extensions.Logging` as the abstraction (already planned)
- Console sink: for CLI (colored, human-readable via Spectre.Console)
- File sink: for daemon (structured JSON, `~/.homelab/logs/daemon-{date}.json`)
- Log rotation: keep last 7 days, max 100MB total

**Config**:
```yaml
logging:
  level: Information                  # Trace, Debug, Information, Warning, Error
  file:
    enabled: true
    path: logs/                       # Relative to homelab-dir
    retention-days: 7
    max-size-mb: 100
  console:
    enabled: true
```

**Implementation**: Use `Microsoft.Extensions.Logging.Console` (already in CPM) + a simple `FileLoggerProvider` (custom, <100 LOC) for JSON file logging. No external package needed.

**Correlation**: Each API request gets a `X-Correlation-Id` header → flows through all `ILogger` calls → appears in file logs. CLI generates a correlation ID per command invocation.

---

### 14.9 API Error Response Format

Standardize on **RFC 9457 Problem Details** (`application/problem+json`).

```csharp
public record ProblemDetails
{
    public required string Type { get; init; }      // "urn:homelab:validation-failed"
    public required string Title { get; init; }     // "Validation Failed"
    public required int Status { get; init; }       // 422
    public string? Detail { get; init; }            // "Port 443 is used by both ingress and core stacks"
    public string? Instance { get; init; }          // "/api/deploy" (the request path)
    public IDictionary<string, object>? Extensions { get; init; }
}
```

**Mapping `Result<T>` to HTTP**:
```csharp
public static IResult ToApiResult<T>(this Result<T> result) => result.Match(
    onSuccess: value => Results.Ok(value),
    onFailure: errors => Results.Problem(new ProblemDetails
    {
        Type = "urn:homelab:operation-failed",
        Title = "Operation Failed",
        Status = 422,
        Detail = string.Join("; ", errors.Select(e => e.ErrorMessage)),
        Extensions = new Dictionary<string, object>
        {
            ["errors"] = errors.Select(e => new { e.ErrorMessage, e.MemberNames }).ToList()
        }
    })
);
```

---

### 14.10 Config Schema Versioning

The `homelab.yml` schema will evolve. Need forward-compatible migration.

```yaml
# First field in every homelab.yml
schema-version: 1

domain: homelab.local
# ... rest of config
```

**Migration strategy**:
```csharp
public interface IConfigMigrator
{
    int FromVersion { get; }
    int ToVersion { get; }
    Result<HomeLabConfig> Migrate(HomeLabConfig old);
}
```

- `homelab validate` checks `schema-version` and warns if outdated
- `homelab config migrate` runs all applicable migrators in sequence
- Migrators are pure functions (testable)
- Breaking changes bump the version number
- Auto-commit after migration: `"config: migrate schema v1 → v2"`

---

### 14.11 Data Volume Strategy

All stateful data lives under `{data-root}` (default `/opt/homelab/data` or `~/.homelab/data`), organized by stack.

```
{data-root}/
├── gitlab/
│   ├── config/          # /etc/gitlab
│   ├── data/            # /var/opt/gitlab
│   └── logs/            # /var/log/gitlab
├── keycloak/
│   └── postgres-data/   # /var/lib/postgresql/data
├── minio/
│   └── data/            # /data
├── traefik/
│   └── certs/           # /letsencrypt (if using ACME)
└── runners/
    └── config/          # /etc/gitlab-runner
```

**Compose volumes use bind mounts** (not named volumes):
```yaml
volumes:
  - "${DATA_ROOT}/gitlab/config:/etc/gitlab"
  - "${DATA_ROOT}/gitlab/data:/var/opt/gitlab"
```

**Why bind mounts over named volumes**: Bind mounts survive machine recreation (`podman machine rm` + `podman machine init`). Named volumes are tied to the machine's internal storage and can be lost.

**Machine volume passthrough**: Machines are created with `--volume` to expose the host data directory:
```yaml
machines:
  - name: homelab
    volumes:
      - "${DATA_ROOT}:/mnt/homelab-data"   # Host data → machine mount point
```

Stack compose files then reference `/mnt/homelab-data/gitlab/config:/etc/gitlab`.

---

### 14.12 Self-Update / GitOps Loop

The homelab's own GitLab can run a CI pipeline that validates and applies config changes — full GitOps.

**Flow**:
1. User edits `homelab.yml` in GitLab Web IDE (or pushes via git)
2. GitLab CI pipeline triggers (`.gitlab-ci.yml` in the config repo):
   ```yaml
   validate:
     script: homelab validate --config homelab.yml

   plan:
     script: homelab deploy --plan-only --config homelab.yml
     when: manual  # Require manual approval for changes

   apply:
     script: homelab deploy --config homelab.yml
     when: manual
     needs: [plan]
   ```
3. Pipeline validates config, shows plan, requires manual approval, then applies

**Daemon watch mode** (alternative to CI-based):
```csharp
public interface IConfigWatcher
{
    /// Watches ~/.homelab/homelab.yml for changes, auto-reconciles
    Task<Result<Unit>> StartAsync(CancellationToken ct);
    Task<Result<Unit>> StopAsync(CancellationToken ct);
}
```

Config:
```yaml
gitops:
  enabled: false                    # Opt-in
  mode: ci-pipeline                 # "ci-pipeline" or "daemon-watch"
  auto-apply: false                 # If true, daemon auto-applies on config change
```

---

### 14.13 Audit Trail

Track every mutation: who, when, what, from where (CLI/API).

**Domain**:
```csharp
public record AuditEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Actor { get; init; }        // "cli:local", "api:admin@keycloak", "gitops"
    public required string Action { get; init; }       // "deploy", "machines.add", "dns.apply"
    public required string Detail { get; init; }       // "Added machine: homelab-ci (podman, 4 CPUs)"
    public string? CorrelationId { get; init; }
}

public interface IAuditLog
{
    Task<Result<Unit>> RecordAsync(AuditEntry entry, CancellationToken ct);
    Task<Result<IReadOnlyList<AuditEntry>>> QueryAsync(AuditQuery query, CancellationToken ct);
}
```

**Infrastructure** — `HomeLab.Infrastructure.FileSystem`:
- `FileAuditLog.cs` — append-only JSON lines file: `~/.homelab/audit.jsonl`
- Git-tracked (auto-committed alongside config changes)
- Queryable by time range, actor, action

**API**:
```
GET /api/audit?from=...&to=...&actor=...&action=...
```

**CLI**:
```
homelab audit [--last N] [--actor <name>] [--action <type>]
```

---

### 14.14 Integration / Smoke Tests

Verify generated compose YAML actually works, beyond unit tests.

**Strategy**: A dedicated test suite (`HomeLab.Tests.Integration`) that:
1. Generates compose files from a test config (using real domain + infra code)
2. Validates YAML syntax (`podman-compose config` dry-run)
3. Optionally spins up a test Podman machine and deploys (CI-only, gated by env var)

```csharp
[Trait("Category", "Integration")]
public class ComposeIntegrationTests
{
    [Fact]
    public async Task GeneratedCoreStack_IsValidCompose()
    {
        var config = HomeLabConfig.Default();
        var stack = new CoreStack();
        var composeFile = await stack.BuildComposeFileAsync(config, CancellationToken.None);
        var yaml = ComposeYamlSerializer.Serialize(composeFile.ValueOrThrow().Resolved());

        // Write to temp file, run: podman-compose -f temp.yml config
        // Assert exit code 0 (valid compose syntax)
    }
}
```

**Gating**: Integration tests are skipped by default (`[Trait]`-based filter). CI runs them in a dedicated stage with a real podman machine.

---

### 14.15 Network Documentation & DNS Resolution Chain

**Resolution chain**:
```
Client (browser) → Pi-hole (DNS) → resolves gitlab.homelab.local → 192.168.1.100
  → Traefik (:443) → routes by Host header → GitLab container (:80)
```

**Cross-machine communication** (CI runner → GitLab):
- CI machine resolves `gitlab.homelab.local` via Pi-hole (same as any LAN client)
- Runner connects to Traefik's `:443` on the service machine's IP
- Runner must trust the self-signed CA (CA cert copied into CI machine)

**Subnet allocation** (explicit, documented):

| Network | Subnet | Gateway | Purpose |
|---------|--------|---------|---------|
| `homelab-frontend` | 172.20.0.0/24 | 172.20.0.1 | Traefik-routable services |
| `homelab-identity` | 172.20.1.0/24 | 172.20.1.1 | Keycloak ↔ its PG |
| `homelab-ci` | 172.20.2.0/24 | 172.20.2.1 | Runners (intra-machine only) |

**Future multi-host**: When services span hosts, use WireGuard mesh (each machine gets a WG interface) or Podman's native network plugins. This is out of scope for Phase 1 but the `IMachineProvider` abstraction and per-machine `IStackDeployer` are designed to support it.

---

## 15. .NET Aspire — Orchestration + Integrations

Two capabilities: (A) an **Aspire AppHost** that orchestrates the entire HomeLab in two modes (processes for dev, containers for deploy), and (B) **NuGet integrations** so external developers can consume the homelab stack from their own Aspire apps.

### 15.0 HomeLab AppHost — Two Launch Modes

New project: **`FrenchExDev.Net.HomeLab.AppHost`** — the Aspire orchestrator for the HomeLab itself.

```
HomeLab/
├── src/
│   └── FrenchExDev.Net.HomeLab.AppHost/     # Aspire AppHost (orchestrator)
```

```xml
<!-- HomeLab.AppHost.csproj -->
<Project Sdk="Aspire.AppHost">
  <!-- Our .NET projects -->
  <ProjectReference Include="../FrenchExDev.Net.HomeLab.Api/..." />
  <!-- Our hosting integrations for third-party services -->
  <ProjectReference Include="../../Aspire/src/FrenchExDev.Net.Aspire.Hosting.GitLab/..." />
  <ProjectReference Include="../../Aspire/src/FrenchExDev.Net.Aspire.Hosting.MinIO/..." />
  <ProjectReference Include="../../Aspire/src/FrenchExDev.Net.Aspire.Hosting.PiHole/..." />
</Project>
```

#### Mode A: Aspire Dev (processes + containers)

`dotnet run --project HomeLab.AppHost` — for **development and debugging**.

- Our .NET code (`HomeLab.Api`) runs as a **debuggable process** (`AddProject`)
- Third-party services (GitLab, Keycloak, MinIO, Traefik) run as **containers** (`AddContainer` / our hosting integrations)
- Aspire dashboard at `localhost:18888` — logs, traces, metrics, resource status
- Hot reload, breakpoints, full IDE debugging on the API daemon

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// ── Third-party: always containers ───────────────────────────
var keycloakPg = builder.AddPostgres("keycloak-pg")
    .WithDataVolume("keycloak-pg-data");

var keycloak = builder.AddKeycloak("keycloak")               // Official Aspire integration
    .WithDataVolume("keycloak-data")
    .WithExternalHttpEndpoints();

var traefik = builder.AddContainer("traefik", "traefik", "v3.3")
    .WithHttpEndpoint(port: 80, targetPort: 80, name: "web")
    .WithHttpsEndpoint(port: 443, targetPort: 443, name: "websecure")
    .WithBindMount("./traefik-config", "/etc/traefik");

var gitlab = builder.AddGitLab("gitlab")                      // Our custom integration
    .WithRegistryEnabled()
    .WithOmnibusConfig(c => c
        .UseExternalKeycloak(keycloak)
        .UseExternalMinIO(minio));

var minio = builder.AddMinIO("minio")                         // Our custom integration
    .WithBuckets("artifacts", "lfs", "uploads")
    .WithDataVolume("minio-data");

// ── Our .NET code: runs as process (debuggable!) ─────────────
var api = builder.AddProject<Projects.HomeLab_Api>("homelab-api")
    .WithReference(keycloak)
    .WithReference(gitlab)
    .WithReference(minio)
    .WaitFor(keycloak)
    .WaitFor(gitlab);

// ── Docker Compose publisher (for Mode B) ────────────────────
builder.AddDockerComposeEnvironment("homelab");

builder.Build().Run();
```

#### Mode B: Container Deployment (generated docker-compose)

`aspire publish --publisher docker-compose` — for **production deployment to the homelab**.

This generates `docker-compose.yml` + `.env` files from the app model:
- Our `HomeLab.Api` gets built as a Docker image and becomes a compose service
- Third-party containers stay as containers
- All connection strings, ports, volumes become environment variable placeholders
- The generated compose file can be deployed via `podman-compose up` on the homelab machine

```bash
# Generate docker-compose.yml from the Aspire app model
cd HomeLab/src/FrenchExDev.Net.HomeLab.AppHost
aspire publish --publisher docker-compose --output-path ~/.homelab/generated/

# Deploy to homelab podman machine
podman-compose --connection homelab -f ~/.homelab/generated/docker-compose.yml up -d
```

**What gets generated**:
```yaml
# docker-compose.yml (auto-generated by aspire publish)
services:
  homelab-api:
    image: homelab-api:latest              # Built from HomeLab.Api project
    ports: ["9443:9443"]
    environment:
      ConnectionStrings__keycloak: ${KEYCLOAK_URL}
      ConnectionStrings__gitlab: ${GITLAB_URL}
      ConnectionStrings__minio: ${MINIO_URL}
    depends_on:
      keycloak: { condition: service_healthy }
      gitlab: { condition: service_healthy }

  keycloak:
    image: quay.io/keycloak/keycloak:26.1
    # ... full config from Aspire model

  gitlab:
    image: gitlab/gitlab-ce:17.8.0-ce.0
    # ... full config including GITLAB_OMNIBUS_CONFIG

  minio:
    image: minio/minio:latest
    # ... buckets, volumes, ports

  traefik:
    image: traefik:v3.3
    # ... entrypoints, providers, cert mounts
```

#### Mode C: Hybrid — Process API + Remote HomeLab Services

For working on the API daemon while pointing at a running homelab:

```csharp
// In AppHost, toggle via configuration
if (builder.Configuration["HomeLab:Mode"] == "remote")
{
    // Connect to running homelab (no local containers)
    var homelab = builder.AddHomeLab("homelab", o => o.ConfigPath = "~/.homelab/homelab.yml");
    var api = builder.AddProject<Projects.HomeLab_Api>("homelab-api")
        .WithReference(homelab.GetService("keycloak"))
        .WithReference(homelab.GetService("gitlab"))
        .WithReference(homelab.GetService("minio"));
}
else
{
    // Full local stack (Mode A above)
    // ...
}
```

#### Integration with HomeLab CLI

The CLI can trigger both modes:

```
homelab dev                    # Starts Aspire AppHost (Mode A) — full dev environment
homelab dev --remote           # Starts Aspire AppHost (Mode C) — API process + remote services
homelab generate --aspire      # Runs aspire publish → generates docker-compose.yml (Mode B)
homelab deploy                 # Deploys generated compose to podman machine
```

#### Customization via `PublishAsDockerComposeService`

Fine-grained control over which resources become containers:

```csharp
// Force the API to run as a process even in compose mode (for debugging on the host)
api.PublishAsDockerComposeService(service =>
{
    service.Image = null;  // Don't containerize — run as process
});

// Customize GitLab's compose service
gitlab.PublishAsDockerComposeService(service =>
{
    service.Volumes.Add("gitlab-data:/var/opt/gitlab");
    service.Labels["traefik.enable"] = "true";
    service.Labels["traefik.http.routers.gitlab.rule"] = "Host(`gitlab.homelab.local`)";
});
```

---

### 15.1 Aspire NuGet Integrations (for external consumers)

Provide NuGet packages so developers can use the homelab stack from their Aspire AppHost — both for **local dev containers** and for **connecting to the running homelab as a dev environment**.

### 15.2 What Aspire Already Has

| Service | Official Aspire Package | Status |
|---------|------------------------|--------|
| PostgreSQL | `Aspire.Hosting.PostgreSQL` | Official |
| Redis | `Aspire.Hosting.Redis` | Official |
| Keycloak | `Aspire.Hosting.Keycloak` | Official |
| MinIO | — | Community example only |
| Traefik | — (Aspire has built-in reverse proxy) | Not needed |
| GitLab | — | None |
| Pi-hole | — | None |
| Podman machines | — | None |

### 15.3 What We Provide — Hosting Integrations

Hosting integrations live in the **AppHost** project. Each provides `builder.AddX()` to declare resources.

**Pattern** (from Aspire docs): extend `ContainerResource`, implement `IResourceWithConnectionString`, provide extension method on `IDistributedApplicationBuilder`.

#### `FrenchExDev.Net.Aspire.Hosting.GitLab`

Spins up GitLab CE container for dev/testing. No official Aspire integration exists.

```csharp
// In AppHost
var gitlab = builder.AddGitLab("gitlab")
    .WithRegistryEnabled()
    .WithOmnibusConfig(c => c.DisableMonitoring());   // Lighter for dev

builder.AddProject<Projects.MyApi>("api")
    .WithReference(gitlab);  // Injects GITLAB_URL, GITLAB_TOKEN as env vars
```

Resource exposes:
- `ConnectionStringExpression` → `https://{host}:{port}` (web URL)
- Named endpoints: `web`, `registry`, `ssh`
- Environment: `GITLAB_URL`, `GITLAB_API_TOKEN`, `GITLAB_REGISTRY_URL`

```
FrenchExDev.Net.Aspire.Hosting.GitLab/
├── GitLabResource.cs                  # : ContainerResource, IResourceWithConnectionString
├── GitLabResourceBuilderExtensions.cs # AddGitLab(), WithRegistryEnabled(), WithOmnibusConfig()
└── GitLabContainerImageTags.cs        # Image + tag constants
```

#### `FrenchExDev.Net.Aspire.Hosting.MinIO`

S3-compatible object storage. Community example exists but no official package.

```csharp
var minio = builder.AddMinIO("storage")
    .WithBuckets("artifacts", "lfs", "uploads");  // Auto-create buckets on startup

builder.AddProject<Projects.MyApi>("api")
    .WithReference(minio);  // Injects S3 endpoint, access key, secret key
```

Resource exposes:
- `ConnectionStringExpression` → `Endpoint=http://{host}:{port};AccessKey={key};SecretKey={secret}`
- Named endpoints: `api` (9000), `console` (9001)
- Health check: `GET /minio/health/live`

#### `FrenchExDev.Net.Aspire.Hosting.PiHole`

Pi-hole container for testing DNS configurations.

```csharp
var pihole = builder.AddPiHole("dns")
    .WithLocalDnsRecords(new Dictionary<string, string>
    {
        ["myapp.dev.local"] = "127.0.0.1",
        ["api.dev.local"] = "127.0.0.1"
    });
```

Resource exposes:
- `ConnectionStringExpression` → `http://{host}:{port}` (admin API)
- Admin password auto-generated
- Can be referenced by `FrenchExDev.Net.Aspire.PiHole` client integration

### 15.4 What We Provide — Client Integrations

Client integrations live in **service projects**. They auto-configure typed clients from Aspire connection strings.

#### `FrenchExDev.Net.Aspire.PiHole`

Registers `PiHoleClient` in DI, auto-configured from Aspire-injected connection string.

```csharp
// In service project
builder.AddPiHoleClient("dns");

// Resolves to:
// services.AddSingleton<PiHoleClient>(sp => {
//     var conn = sp.GetRequiredService<IConfiguration>()["ConnectionStrings:dns"];
//     return new PiHoleClient(httpClient, PiHoleOptions.Parse(conn));
// });
```

Includes:
- Health check registration (`IHealthCheck`)
- OpenTelemetry tracing for API calls
- Configurable retry/timeout via `PiHoleClientSettings`

#### `FrenchExDev.Net.Aspire.GitLab`

Registers a GitLab API client from Aspire connection string.

```csharp
builder.AddGitLabClient("gitlab");
// Auto-configures: base URL, API token, registry URL
```

#### `FrenchExDev.Net.Aspire.MinIO`

Registers an S3-compatible client from Aspire connection string.

```csharp
builder.AddMinIOClient("storage");
// Auto-configures: endpoint, access key, secret key, region
```

### 15.5 The Big One: `FrenchExDev.Net.Aspire.Hosting.HomeLab`

**Connect your Aspire app to your running homelab** instead of spinning up local containers. Your homelab becomes the dev environment.

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Connect to running homelab instance
var homelab = builder.AddHomeLab("homelab", options =>
{
    options.ConfigPath = "~/.homelab/homelab.yml";  // Reads running config
    options.DnsZone = "homelab.local";              // Which zone to use
    options.ApiUrl = "https://api.homelab.local:9443";
    options.ApiKey = builder.Configuration["HomeLab:ApiKey"];
});

// Reference services from the running homelab — no containers spun up
var keycloak = homelab.AddReference("keycloak");   // auth.homelab.local
var gitlab = homelab.AddReference("gitlab");        // gitlab.homelab.local
var minio = homelab.AddReference("minio");          // minio.homelab.local

// Your app wires into the real homelab services
builder.AddProject<Projects.MyApi>("api")
    .WithReference(keycloak)    // OIDC issuer = https://auth.homelab.local
    .WithReference(gitlab)      // GitLab API = https://gitlab.homelab.local
    .WithReference(minio);      // S3 endpoint = https://minio.homelab.local

builder.Build().Run();
```

**How it works**:
1. Reads `homelab.yml` to discover service URLs, ports, connection details
2. Creates `ExternalResource` references (not containers — no local overhead)
3. Injects real connection strings from the running homelab
4. The homelab's PKI root CA must be trusted on the dev machine (`homelab pki trust`)
5. DNS resolves via Pi-hole (or hosts file cloaking)

**`HomeLabResource`**:
```csharp
public class HomeLabResource(string name, HomeLabConfig config)
    : Resource(name)
{
    public HomeLabConfig Config { get; } = config;

    /// Creates a reference to a running homelab service
    public IResourceBuilder<ExternalServiceResource> AddReference(
        IDistributedApplicationBuilder builder, string serviceName)
    {
        // Maps service name to URL from config:
        // "keycloak" → https://auth.{domain}
        // "gitlab" → https://gitlab.{domain}
        // "minio" → https://minio.{domain}:9000
    }
}
```

**Why this is powerful**:
- Dev environment matches your actual infra (not toy containers)
- Keycloak OIDC with real realm/clients — no setup needed
- GitLab API with real repos — test webhooks, CI integration
- MinIO with real buckets — test S3 operations
- No 8GB+ RAM consumed by local GitLab/Keycloak containers
- Switch between local containers and homelab by changing one line

### 15.6 Project Structure

New sibling solution (or part of HomeLab solution):

```
Aspire/
├── FrenchExDev.Net.Aspire.slnx
├── src/
│   ├── FrenchExDev.Net.Aspire.Hosting.GitLab/       # builder.AddGitLab()
│   ├── FrenchExDev.Net.Aspire.Hosting.MinIO/         # builder.AddMinIO()
│   ├── FrenchExDev.Net.Aspire.Hosting.PiHole/        # builder.AddPiHole()
│   ├── FrenchExDev.Net.Aspire.Hosting.HomeLab/       # builder.AddHomeLab() — connect to running homelab
│   ├── FrenchExDev.Net.Aspire.PiHole/                # Client: AddPiHoleClient()
│   ├── FrenchExDev.Net.Aspire.GitLab/                # Client: AddGitLabClient()
│   └── FrenchExDev.Net.Aspire.MinIO/                 # Client: AddMinIOClient()
└── test/
    └── FrenchExDev.Net.Aspire.Tests/
```

**Dependencies**:
- `Aspire.Hosting` (for hosting integrations)
- `FrenchExDev.Net.PiHole` (for PiHole client integration)
- `FrenchExDev.Net.HomeLab` (shared kernel — for `HomeLabConfig` in the HomeLab hosting integration)

**NuGet publishing**: Each integration is a separate NuGet package, following the Aspire naming convention.

### 15.7 Implementation Phase

After HomeLab core is working (Phase 15+):
- Phase 16a: `Aspire.Hosting.GitLab` + `Aspire.GitLab`
- Phase 16b: `Aspire.Hosting.MinIO` + `Aspire.MinIO`
- Phase 16c: `Aspire.Hosting.PiHole` + `Aspire.PiHole`
- Phase 16d: `Aspire.Hosting.HomeLab` (the connector to running homelab)

Sources:
- [Create custom hosting integrations](https://aspire.dev/integrations/custom-integrations/hosting-integrations/)
- [Create custom client integrations](https://learn.microsoft.com/en-us/dotnet/aspire/extensibility/custom-client-integration)
- [Aspire Community Toolkit](https://github.com/CommunityToolkit/Aspire)
- [Aspire Integrations Overview](https://aspire.dev/integrations/overview/)
- [Keycloak integration](https://learn.microsoft.com/en-us/dotnet/aspire/authentication/keycloak-integration)
- [Custom MinIO integration example](https://medium.com/@manuel_gabteni/creating-a-net-aspire-hosting-integration-for-minio-s3-compatible-buckets-c69b50118e8b)

---

## Critical Files

| File | Action |
|------|--------|
| `HttpClient/src/.../Code.cs` | **Modify**: add POST/DELETE/Send |
| `HttpClient/src/.../Testing/Fakes.cs` | **Modify**: update FakeHttpClient |
| `Podman/.../PodmanClient` | Reuse: Machine.* commands |
| `PodmanCompose/.../PodmanComposeClient` | Reuse: Up/Down/Ps |
| `DockerCompose/.../Bundle/` | Reuse: ComposeFile builders |
| `Result`, `Builder` | Reuse: Result<T>, AbstractBuilder<T> |
| `QualityGate/.../Config/` | Pattern: YAML config |
| `QualityGate/.../Cli/Program.cs` | Pattern: System.CommandLine |
| `FrenchExDev.Net.slnx` | Add PiHole + HomeLab |
| `Directory.Packages.props` | Add `MediatR.Contracts`, `MediatR`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting` |

## Verification

1. `dotnet build` both solutions
2. `dotnet test` both solutions
3. `homelab init` → `~/.homelab/` with git repo + `homelab.yml`
4. `homelab validate` → validates multi-zone, multi-runner, multi-provider config
5. `homelab machines plan` → shows actions for podman + ssh machines
6. `homelab pki init` → CA + wildcard cert (covering all zone SANs)
7. `homelab dns apply` → Pi-hole records for both zones
8. `homelab generate` → compose YAML with per-runner services
9. `homelab daemon start` → API at :9443
10. `curl https://localhost:9443/api/status` → full status JSON
11. `curl -X POST https://localhost:9443/api/machines -d '{...}'` → adds machine to config + auto-commits
12. Quality gates pass
