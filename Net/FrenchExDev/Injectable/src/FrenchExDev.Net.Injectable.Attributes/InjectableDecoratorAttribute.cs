namespace FrenchExDev.Net.Injectable.Attributes;

/// <summary>
/// Marks a class as a decorator for the specified service type.
/// The decorator wraps the existing registration and is resolved in <see cref="Order"/> sequence.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class InjectableDecoratorAttribute : Attribute
{
    /// <summary>The service type (interface) this decorator wraps.</summary>
    public Type ServiceType { get; }

    /// <summary>
    /// Ordering hint when multiple decorators target the same service.
    /// Lower values are applied first (innermost). Default is 0.
    /// </summary>
    public int Order { get; set; }

    public InjectableDecoratorAttribute(Type serviceType)
    {
        ServiceType = serviceType;
    }
}
