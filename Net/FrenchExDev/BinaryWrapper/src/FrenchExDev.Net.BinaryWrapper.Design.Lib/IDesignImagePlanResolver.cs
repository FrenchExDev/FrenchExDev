namespace FrenchExDev.Net.BinaryWrapper.Design.Lib;

/// <summary>Selects a complete installation recipe for a software version.</summary>
public interface IDesignImagePlanResolver
{
    /// <summary>All available recipes, sharing one ImageName. The collection must remain stable during a run.</summary>
    IReadOnlyList<DesignImagePlan> Plans { get; }

    /// <summary>Returns one of the instances declared in Plans for the supplied collector version.</summary>
    DesignImagePlan Resolve(string version);
}

/// <summary>Uses the same recipe for every version.</summary>
public sealed class SingleDesignImagePlanResolver : IDesignImagePlanResolver
{
    public IReadOnlyList<DesignImagePlan> Plans { get; }

    public SingleDesignImagePlanResolver(DesignImagePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Plans = Array.AsReadOnly(new[] { plan });
    }

    public DesignImagePlan Resolve(string version) => Plans[0];
}
