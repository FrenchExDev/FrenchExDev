namespace FrenchExDev.Net.Injectable.SourceGenerator.Lib;

/// <summary>Intermediate model for an interface decorated with [Injectable].</summary>
public sealed class InterfaceContractModel
{
    public InterfaceContractModel(string interfaceSymbolName, string interfaceTypeFull, string scope, string? key, bool tryAdd, bool isOpenGeneric)
    {
        InterfaceSymbolName = interfaceSymbolName;
        InterfaceTypeFull = interfaceTypeFull;
        Scope = scope;
        Key = key;
        TryAdd = tryAdd;
        IsOpenGeneric = isOpenGeneric;
    }

    /// <summary>Display name used to match implementors (e.g. <c>MyApp.IFoo</c>).</summary>
    public string InterfaceSymbolName { get; }

    /// <summary>Fully qualified name for code generation (e.g. <c>global::MyApp.IFoo</c>).</summary>
    public string InterfaceTypeFull { get; }

    /// <summary>Scope declared on the interface — the contract all implementations must follow.</summary>
    public string Scope { get; }

    /// <summary>Optional key for keyed registration.</summary>
    public string? Key { get; }

    /// <summary>Whether implementations should use TryAdd.</summary>
    public bool TryAdd { get; }

    /// <summary>Whether the interface is an open generic.</summary>
    public bool IsOpenGeneric { get; }
}
