namespace FrenchExDev.Net.Injectable.SourceGenerator.Lib;

public sealed class InjectableDecoratorModel
{
    public InjectableDecoratorModel(
        string decoratorTypeFull,
        string serviceTypeFull,
        int order = 0)
    {
        DecoratorTypeFull = decoratorTypeFull;
        ServiceTypeFull = serviceTypeFull;
        Order = order;
    }

    /// <summary>Fully qualified decorator implementation type.</summary>
    public string DecoratorTypeFull { get; }

    /// <summary>Fully qualified service type being decorated.</summary>
    public string ServiceTypeFull { get; }

    /// <summary>Ordering hint. Lower = innermost (applied first).</summary>
    public int Order { get; }
}
