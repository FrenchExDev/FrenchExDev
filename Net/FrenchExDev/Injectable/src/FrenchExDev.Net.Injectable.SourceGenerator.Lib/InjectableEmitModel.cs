using System.Collections.Generic;
using System.Linq;

namespace FrenchExDev.Net.Injectable.SourceGenerator.Lib;

public sealed class InjectableEmitModel
{
    public InjectableEmitModel(
        string assemblyName,
        IReadOnlyList<InjectableServiceModel> services,
        IReadOnlyList<InjectableDecoratorModel>? decorators = null)
    {
        AssemblyName = assemblyName;
        Services = services;
        Decorators = decorators ?? new List<InjectableDecoratorModel>();
    }

    /// <summary>Assembly name, used to derive the extension method name.</summary>
    public string AssemblyName { get; }

    /// <summary>All injectable services discovered in the assembly.</summary>
    public IReadOnlyList<InjectableServiceModel> Services { get; }

    /// <summary>All decorator registrations discovered in the assembly, ordered by <see cref="InjectableDecoratorModel.Order"/>.</summary>
    public IReadOnlyList<InjectableDecoratorModel> Decorators { get; }

    /// <summary>True if any service uses keyed registration.</summary>
    public bool HasKeyedServices => Services.Any(s => s.Key is not null);

    /// <summary>True if any service uses open generic registration.</summary>
    public bool HasOpenGenerics => Services.Any(s => s.IsOpenGeneric);

    /// <summary>True if any service uses TryAdd registration.</summary>
    public bool HasTryAdd => Services.Any(s => s.TryAdd);
}
