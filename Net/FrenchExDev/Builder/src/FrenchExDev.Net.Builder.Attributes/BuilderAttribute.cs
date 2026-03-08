namespace FrenchExDev.Net.Builder.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class BuilderAttribute : Attribute
{
    /// <summary>
    /// When set, the generated builder inherits from AbstractBuilder&lt;T, TException&gt;
    /// and exposes InstantiateAsync(CancellationToken).
    /// When null (default), the generated builder inherits from AbstractBuilder&lt;T&gt;
    /// and exposes CreateAsync(CancellationToken).
    /// </summary>
    public Type? Exception { get; set; }
}
