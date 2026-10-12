using System.Collections.Generic;

namespace FrenchExDev.Net.Injectable.SourceGenerator.Lib;

public sealed class InjectableServiceModel
{
    public InjectableServiceModel(
        string implementationTypeFull,
        IReadOnlyList<string> serviceTypesFull,
        string scope,
        string? key = null,
        bool tryAdd = false,
        bool isOpenGeneric = false,
        bool scopeIsExplicit = true)
    {
        ImplementationTypeFull = implementationTypeFull;
        ServiceTypesFull = serviceTypesFull;
        Scope = scope;
        Key = key;
        TryAdd = tryAdd;
        IsOpenGeneric = isOpenGeneric;
        ScopeIsExplicit = scopeIsExplicit;
    }

    /// <summary>Fully qualified implementation type (e.g. <c>global::MyApp.Foo</c>).</summary>
    public string ImplementationTypeFull { get; }

    /// <summary>
    /// Fully qualified service types to register as.
    /// Empty means register as self only.
    /// </summary>
    public IReadOnlyList<string> ServiceTypesFull { get; }

    /// <summary><c>"Singleton"</c>, <c>"Scoped"</c>, or <c>"Transient"</c>.</summary>
    public string Scope { get; }

    /// <summary>Service key for keyed DI (.NET 8+). Null means standard (non-keyed) registration.</summary>
    public string? Key { get; }

    /// <summary>When true, generates <c>TryAdd</c> variants to avoid duplicate registrations.</summary>
    public bool TryAdd { get; }

    /// <summary>When true, uses <c>typeof()</c> registration for open generic types.</summary>
    public bool IsOpenGeneric { get; }

    /// <summary>True when the developer explicitly set <c>Scope</c> on the attribute (vs. relying on default).</summary>
    public bool ScopeIsExplicit { get; }

    /// <summary>Returns true if scope was NOT explicitly set and can be overridden by assembly defaults.</summary>
    public bool HasExplicitScope() => ScopeIsExplicit;
}
