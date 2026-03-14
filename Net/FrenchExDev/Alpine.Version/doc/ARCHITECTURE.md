# Alpine.Version - Architecture Documentation

## Table of Contents

- [Overview](#overview)
- [Architecture Diagrams](#architecture-diagrams)
- [Core Components](#core-components)
- [Sequence Diagrams](#sequence-diagrams)
- [Design Decisions](#design-decisions)
- [Best Practices](#best-practices)

---

## Overview

Alpine.Version is a .NET 9 library that provides comprehensive functionality for working with Alpine Linux version information. The architecture is designed around three main pillars:

1. **Version Representation & Comparison** - Parse and compare Alpine versions with support for semantic versioning, release candidates, and edge releases
2. **Repository Search** - Query the official Alpine Linux CDN with powerful filtering capabilities
3. **Result Aggregation** - Collect and return version, architecture, flavor, and checksum information

**Key Design Principles:**
- **Immutability** - Version objects are read-only after creation
- **Fluent API** - Builder pattern for search filters
- **Async-First** - All I/O operations are asynchronous
- **Parallel Processing** - Concurrent HTTP requests for performance
- **Separation of Concerns** - Clear boundaries between parsing, comparison, and search

---

## Architecture Diagrams

### Component Architecture

```mermaid
classDiagram
    class AlpineVersion {
        <<Core>>
        +string Major
        +string Minor
        +string Patch
        +bool IsEdge
        +int MajorNumber
        +int MinorNumber
        +int PatchNumber
        +int RcNumber
        +From(string) AlpineVersion$
        +CompareTo(AlpineVersion) int
        +Compare(AlpineVersion, Operator, AlpineVersion) bool$
        +ToString() string
        +ToMajorMinor() string
        +ToMajorMinorUrl() string
    }
    
    class MajorMinorPatchRelationalComparer {
        <<Comparer>>
        +Compare(AlpineVersion, AlpineVersion) int
    }
    
    class IAlpineVersionSearcher {
        <<interface>>
        +SearchAsync(AlpineVersionSearchingFilters) Task~AlpineVersionList~
        +SearchAsync(Action~AlpineVersionSearchingFiltersBuilder~) Task~AlpineVersionList~
    }
    
    class AlpineVersionSearcher {
        <<Service>>
        -IHttpClient _httpClient
        +SearchAsync(AlpineVersionSearchingFilters) Task~AlpineVersionList~
        -QueryWebForAlpineVersionsForArchitecture() Task
        -QueryAlpineVersionsMatchingFilters() Task
        -GetShaRecordsAsync() Task
    }
    
    class AlpineVersionSearchingFiltersBuilder {
        <<Builder>>
        +WithMinimumVersion(string) AlpineVersionSearchingFiltersBuilder
        +WithMaximumVersion(string) AlpineVersionSearchingFiltersBuilder
        +WithExactVersion(string) AlpineVersionSearchingFiltersBuilder
        +WithArchitectures(AlpineArchitectures[]) AlpineVersionSearchingFiltersBuilder
        +WithFlavors(AlpineFlavors[]) AlpineVersionSearchingFiltersBuilder
        +WithRc(bool) AlpineVersionSearchingFiltersBuilder
        +Build() AlpineVersionSearchingFilters
    }
    
    class AlpineVersionSearchingFilters {
        <<Filters>>
        +AlpineVersion MinimumVersion
        +AlpineVersion MaximumVersion
        +AlpineVersion ExactVersion
        +AlpineArchitectures[] Architectures
        +AlpineFlavors[] Flavors
        +bool Rc
    }
    
    class AlpineVersionList {
        <<Result>>
        +List~AlpineVersionArchFlavorRecord~ Records
        +Count int
        +this[int] AlpineVersionArchFlavorRecord
    }
    
    class AlpineVersionArchFlavorRecord {
        <<Record>>
        +string Version
        +string Architecture
        +string Flavor
        +string Url
        +string Sha256
        +string Sha512
        +AsAlpineVersion() AlpineVersion
    }
    
    AlpineVersion ..> MajorMinorPatchRelationalComparer : uses
    AlpineVersionSearcher ..|> IAlpineVersionSearcher : implements
    AlpineVersionSearcher ..> AlpineVersionSearchingFilters : uses
    AlpineVersionSearcher ..> AlpineVersionList : creates
    AlpineVersionSearchingFiltersBuilder ..> AlpineVersionSearchingFilters : builds
    AlpineVersionSearchingFilters ..> AlpineVersion : contains
    AlpineVersionList ..> AlpineVersionArchFlavorRecord : contains
    AlpineVersionArchFlavorRecord ..> AlpineVersion : creates
    
    style AlpineVersion fill:#e3f2fd,stroke:#1976d2,stroke-width:2px,color:#000
    style MajorMinorPatchRelationalComparer fill:#e3f2fd,stroke:#1976d2,stroke-width:2px,color:#000
    style IAlpineVersionSearcher fill:#fff3e0,stroke:#f57c00,stroke-width:2px,color:#000
    style AlpineVersionSearcher fill:#e8f5e9,stroke:#388e3c,stroke-width:2px,color:#000
    style AlpineVersionSearchingFiltersBuilder fill:#fff9c4,stroke:#f9a825,stroke-width:2px,color:#000
    style AlpineVersionSearchingFilters fill:#fff9c4,stroke:#f9a825,stroke-width:2px,color:#000
    style AlpineVersionList fill:#fce4ec,stroke:#c2185b,stroke-width:2px,color:#000
    style AlpineVersionArchFlavorRecord fill:#fce4ec,stroke:#c2185b,stroke-width:2px,color:#000
```

---

## Sequence Diagrams

### Version Search Workflow

```mermaid
%%{init: {'theme':'base', 'themeVariables': { 'fontFamily':'arial','fontSize':'14px','primaryColor':'#fff','primaryTextColor':'#000','primaryBorderColor':'#333','lineColor':'#666','secondaryColor':'#fff','tertiaryColor':'#fff','noteTextColor':'#000','noteBkgColor':'#fff','noteBorderColor':'#333','actorBkg':'#f4f4f4','actorBorder':'#333','actorTextColor':'#000','actorLineColor':'#333','signalColor':'#666','signalTextColor':'#000','labelBoxBkgColor':'#f4f4f4','labelBoxBorderColor':'#333','labelTextColor':'#000','loopTextColor':'#000','activationBorderColor':'#333','activationBkgColor':'#e8e8e8','sequenceNumberColor':'#000','altLabelBkgColor':'#f4f4f4','altLabelBorderColor':'#333'}}}%%
sequenceDiagram
    participant Client
    participant Searcher as AlpineVersionSearcher
    participant Builder as FiltersBuilder
    participant CDN as Alpine CDN
    participant Filters as Search Filters
    participant Results as AlpineVersionList
    
    rect rgb(230, 245, 255)
    Note over Client,Filters: Filter Configuration Phase
    Client->>Builder: new FiltersBuilder()
    Client->>Builder: WithMinimumVersion("3.18.0")
    Client->>Builder: WithArchitectures([x86_64])
    Client->>Builder: WithFlavors([standard])
    Client->>Builder: WithRc(false)
    Builder->>Filters: Build()
    Filters-->>Client: AlpineVersionSearchingFilters
    end
    
    rect rgb(255, 245, 230)
    Note over Client,CDN: Version Discovery Phase
    Client->>Searcher: SearchAsync(filters)
    Searcher->>CDN: GET /alpine/
    CDN-->>Searcher: HTML with version list
    Searcher->>Searcher: Parse version URLs (regex)
    Searcher->>Searcher: Filter by min/max version
    Searcher->>Searcher: Apply version range logic
    end
    
    rect rgb(245, 255, 230)
    Note over Searcher,CDN: Architecture Query Phase (Parallel)
    par For each version
        loop For each version
            Searcher->>CDN: GET /alpine/v3.18/releases/
            CDN-->>Searcher: HTML with architecture list
            Searcher->>Searcher: Parse architecture URLs
            Searcher->>Searcher: Filter by included architectures
        end
    end
    end
    
    rect rgb(255, 235, 245)
    Note over Searcher,CDN: Flavor Query Phase (Parallel)
    par For each version-architecture
        loop For each version-arch pair
            Searcher->>CDN: GET /alpine/v3.18/releases/x86_64/
            CDN-->>Searcher: HTML with ISO files
            Searcher->>Searcher: Parse ISO filenames (regex)
            Searcher->>Searcher: Extract flavor and version
            Searcher->>Searcher: Filter by included flavors
            Searcher->>Searcher: Apply RC filter
            
            alt SHA checksums needed
                par Fetch checksums
                    Searcher->>CDN: GET alpine-standard-3.18.0-x86_64.iso.sha256
                    Searcher->>CDN: GET alpine-standard-3.18.0-x86_64.iso.sha512
                end
                CDN-->>Searcher: SHA256 checksum
                CDN-->>Searcher: SHA512 checksum
            end
        end
    end
    end
    
    rect rgb(230, 255, 245)
    Note over Searcher,Results: Result Aggregation Phase
    Searcher->>Searcher: Aggregate concurrent results
    Searcher->>Results: Create AlpineVersionList
    Searcher->>Results: Sort by version (ascending)
    Results-->>Client: AlpineVersionList
    end
    
    Client->>Client: Process results
```

### Version Comparison Workflow

```mermaid
%%{init: {'theme':'base', 'themeVariables': { 'fontFamily':'arial','fontSize':'14px','primaryColor':'#fff','primaryTextColor':'#000','primaryBorderColor':'#333','lineColor':'#666','secondaryColor':'#fff','tertiaryColor':'#fff','noteTextColor':'#000','noteBkgColor':'#fff','noteBorderColor':'#333','actorBkg':'#f4f4f4','actorBorder':'#333','actorTextColor':'#000','actorLineColor':'#333','signalColor':'#666','signalTextColor':'#000','labelBoxBkgColor':'#f4f4f4','labelBoxBorderColor':'#333','labelTextColor':'#000','loopTextColor':'#000','activationBorderColor':'#333','activationBkgColor':'#e8e8e8','sequenceNumberColor':'#000','altLabelBkgColor':'#f4f4f4','altLabelBorderColor':'#333'}}}%%
sequenceDiagram
    participant Client
    participant V1 as AlpineVersion v1
    participant V2 as AlpineVersion v2
    participant Comparer as MajorMinorPatchComparer
    
    rect rgb(230, 245, 255)
    Note over Client,V2: Version Parsing Phase
    Client->>V1: From("3.18.2")
    V1->>V1: Parse version string
    V1->>V1: Set Major=3, Minor=18, Patch=2
    V1-->>Client: AlpineVersion object
    
    Client->>V2: From("3.19.0_rc1")
    V2->>V2: Parse version string
    V2->>V2: Set Major=3, Minor=19, Patch=0_rc1
    V2->>V2: Detect RC number=1
    V2-->>Client: AlpineVersion object
    end
    
    rect rgb(255, 250, 230)
    Note over Client,Comparer: Comparison Logic Phase
    Client->>V1: CompareTo(v2)
    V1->>Comparer: Compare(v1, v2)
    
    Note over Comparer: Step 1: Check for edge versions
    Comparer->>Comparer: Neither is edge, continue
    
    Note over Comparer: Step 2: Compare Major (3 vs 3)
    Comparer->>Comparer: Major equal, continue
    
    Note over Comparer: Step 3: Compare Minor (18 vs 19)
    Comparer->>Comparer: 18 < 19, result determined
    
    Comparer-->>V1: Return -1
    V1-->>Client: -1 (v1 < v2)
    end
```

### Parallel HTTP Requests Flow

```mermaid
%%{init: {'theme':'base', 'themeVariables': { 'fontFamily':'arial','fontSize':'14px','primaryColor':'#fff','primaryTextColor':'#000','primaryBorderColor':'#333','lineColor':'#666','secondaryColor':'#fff','tertiaryColor':'#fff','noteTextColor':'#000','noteBkgColor':'#fff','noteBorderColor':'#333','actorBkg':'#f4f4f4','actorBorder':'#333','actorTextColor':'#000','actorLineColor':'#333','signalColor':'#666','signalTextColor':'#000','labelBoxBkgColor':'#f4f4f4','labelBoxBorderColor':'#333','labelTextColor':'#000','loopTextColor':'#000','activationBorderColor':'#333','activationBkgColor':'#e8e8e8','sequenceNumberColor':'#000','altLabelBkgColor':'#f4f4f4','altLabelBorderColor':'#333'}}}%%
sequenceDiagram
    participant Searcher as AlpineVersionSearcher
    participant Parallel as Parallel.ForEachAsync
    participant CDN1 as CDN Request 1
    participant CDN2 as CDN Request 2
    participant CDNN as CDN Request N
    participant Results as ConcurrentBag
    
    rect rgb(255, 245, 230)
    Note over Searcher,Parallel: Parallel Execution Setup
    Searcher->>Parallel: ForEachAsync(versions, maxParallel=10)
    Parallel->>Parallel: Create task pool
    end
    
    rect rgb(245, 255, 230)
    Note over Parallel,Results: Concurrent HTTP Requests
    par Parallel Requests (Max 10)
        Parallel->>CDN1: GET /alpine/v3.18/releases/x86_64/
        Parallel->>CDN2: GET /alpine/v3.19/releases/x86_64/
        Parallel->>CDNN: GET /alpine/v3.17/releases/x86_64/
    end
    
    CDN1-->>Parallel: HTML response
    Parallel->>Parallel: Parse ISO list
    Parallel->>Results: Add(record1)
    
    CDN2-->>Parallel: HTML response
    Parallel->>Parallel: Parse ISO list
    Parallel->>Results: Add(record2)
    
    CDNN-->>Parallel: HTML response
    Parallel->>Parallel: Parse ISO list
    Parallel->>Results: Add(recordN)
    end
    
    rect rgb(230, 255, 245)
    Note over Parallel,Results: Aggregation Phase
    Parallel->>Searcher: All tasks complete
    Searcher->>Results: ToList()
    Searcher->>Searcher: OrderBy(version)
    end
```

---

## Core Components

### AlpineVersion

**Purpose:** Represents an Alpine Linux version with parsing and comparison capabilities.

**Responsibilities:**
- Parse version strings (e.g., "3.18.2", "edge", "3.19.0_rc1")
- Provide access to version components (major, minor, patch, RC)
- Implement IComparable for sorting
- Support comparison operators
- Handle special cases ("edge", release candidates)

**Key Methods:**
```csharp
public class AlpineVersion : IComparable<AlpineVersion>
{
    // Factory method for parsing
    public static AlpineVersion From(string versionString);
    
    // Comparison
    public int CompareTo(AlpineVersion? other);
    public static bool Compare(AlpineVersion left, Operator @operator, AlpineVersion right);
    
    // String representations
    public override string ToString();              // "3.18.2"
    public string ToMajorMinor();                  // "3.18"
    public string ToMajorMinorUrl();               // "v3.18" or "edge"
}
```

**Design Pattern:** Value Object with Factory Method

---

### AlpineVersionSearcher

**Purpose:** Search the Alpine Linux CDN for available versions, architectures, and flavors.

**Responsibilities:**
- Query Alpine Linux CDN endpoints
- Parse HTML responses using regular expressions
- Apply search filters (version, architecture, flavor, RC)
- Execute parallel HTTP requests
- Aggregate results with checksums
- Return sorted, filtered results

**Key Methods:**
```csharp
public class AlpineVersionSearcher : IAlpineVersionSearcher
{
    // Fluent API entry point
    public Task<AlpineVersionList> SearchAsync(
        Action<AlpineVersionSearchingFiltersBuilder> configureSearchFilter,
        CancellationToken cancellationToken = default);
    
    // Direct filter usage
    public Task<AlpineVersionList> SearchAsync(
        AlpineVersionSearchingFilters alpineVersionSearchingFilters,
        CancellationToken cancellationToken = default);
}
```

**Design Pattern:** Service with Dependency Injection (IHttpClient)

---

### AlpineVersionSearchingFiltersBuilder

**Purpose:** Fluent builder for creating search filter configurations.

**Responsibilities:**
- Provide fluent API for filter configuration
- Validate filter combinations
- Build immutable filter objects
- Parse version strings into AlpineVersion objects

**Key Methods:**
```csharp
public class AlpineVersionSearchingFiltersBuilder
{
    public AlpineVersionSearchingFiltersBuilder WithMinimumVersion(string version);
    public AlpineVersionSearchingFiltersBuilder WithMaximumVersion(string version);
    public AlpineVersionSearchingFiltersBuilder WithExactVersion(string version);
    public AlpineVersionSearchingFiltersBuilder WithArchitectures(AlpineArchitectures[] architectures);
    public AlpineVersionSearchingFiltersBuilder WithFlavors(AlpineFlavors[] flavors);
    public AlpineVersionSearchingFiltersBuilder WithRc(bool includeRc);
    public AlpineVersionSearchingFilters Build();
}
```

**Design Pattern:** Fluent Builder

---

## Design Decisions

### Decision 1: String-Based Version Components

**Context:** Alpine versions include "edge" and various formats that don't fit standard semantic versioning.

**Decision:** Use string for major, minor, and patch components instead of integers.

**Rationale:**
- Supports "edge" as a valid major version
- Handles RC suffixes naturally ("0_rc1")
- Avoids parsing errors for unconventional formats
- Provides numeric accessors (MajorNumber, MinorNumber) when needed

**Alternatives Considered:**
- **Numeric-only with special flags** - Rejected: Complex edge cases
- **Separate classes for edge/stable** - Rejected: Complicates comparison logic

---

### Decision 2: Parallel HTTP Requests with Limits

**Context:** Searching multiple versions, architectures, and flavors requires many HTTP requests.

**Decision:** Use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 10`.

**Rationale:**
- Significantly improves search performance (10x faster than sequential)
- Limits concurrent connections to avoid overwhelming the CDN
- Leverages async/await for efficient resource usage
- ConcurrentBag provides thread-safe result aggregation

**Alternatives Considered:**
- **Sequential requests** - Rejected: Too slow for multi-version queries
- **Unlimited parallelism** - Rejected: Could overwhelm CDN or client
- **Task.WhenAll batching** - Rejected: More complex, similar performance

---

### Decision 3: Fluent Builder for Search Filters

**Context:** Search filters have many optional parameters with complex combinations.

**Decision:** Implement fluent builder pattern for filter configuration.

**Rationale:**
- Improves API discoverability
- Makes optional parameters explicit
- Enables method chaining for readable code
- Validates filter combinations at build time
- Separates filter configuration from search execution

**Alternatives Considered:**
- **Constructor with optional parameters** - Rejected: Too many parameters
- **Property initialization** - Rejected: Less discoverable, no validation

---

## Best Practices

### ? Do

1. **Use Fluent API for Searches**
   ```csharp
   var results = await searcher.SearchAsync(filters => filters
       .WithMinimumVersion("3.18.0")
       .WithArchitectures(new[] { AlpineArchitectures.x86_64 })
       .WithRc(false)
   );
   ```

2. **Handle CancellationToken**
   ```csharp
   var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
   var results = await searcher.SearchAsync(filters => /*...*/, cts.Token);
   ```

3. **Reuse HttpClient**
   ```csharp
   // Create once, use throughout application lifetime
   var httpClient = new StandardHttpClient();
   var searcher = new AlpineVersionSearcher(httpClient);
   ```

### ? Don't

1. **Don't Create HttpClient Per Request**
   ```csharp
   // ? Bad - creates new HttpClient each time
   foreach (var search in searches)
   {
       var httpClient = new StandardHttpClient();
       var searcher = new AlpineVersionSearcher(httpClient);
       await searcher.SearchAsync(/*...*/);
   }
   
   // ? Good - reuse HttpClient
   var httpClient = new StandardHttpClient();
   var searcher = new AlpineVersionSearcher(httpClient);
   foreach (var search in searches)
   {
       await searcher.SearchAsync(/*...*/);
   }
   ```

2. **Don't Ignore Async**
   ```csharp
   // ? Bad - blocks thread
   var results = searcher.SearchAsync(/*...*/).Result;
   
   // ? Good - truly async
   var results = await searcher.SearchAsync(/*...*/);
   ```

3. **Don't Compare Strings Directly**
   ```csharp
   // ? Bad - string comparison doesn't follow semantic versioning
   if (version1.ToString().CompareTo(version2.ToString()) > 0)
   
   // ? Good - use version comparison
   if (version1.CompareTo(version2) > 0)
   ```

---

**Version**: 1.0  
**Last Updated**: January 2025  
**Target Framework**: .NET 9  
**Architecture Style**: Service-Oriented with Builder Pattern
