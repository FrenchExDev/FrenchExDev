using FrenchExDev.Net.Injectable.Microsoft.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FrenchExDev.Net.Injectable.Microsoft.Tests;

public class MicrosoftInjectableTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddFrenchExDevNetInjectableMicrosoftTestsInjectables();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Singleton_is_registered_as_interface()
    {
        using var provider = BuildProvider();
        var service = provider.GetService<ISingletonService>();
        Assert.NotNull(service);
        Assert.IsType<SingletonService>(service);
    }

    [Fact]
    public void Singleton_returns_same_instance()
    {
        using var provider = BuildProvider();
        var a = provider.GetRequiredService<ISingletonService>();
        var b = provider.GetRequiredService<ISingletonService>();
        Assert.Same(a, b);
    }

    [Fact]
    public void Scoped_is_registered_as_interface()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetService<IScopedService>();
        Assert.NotNull(service);
        Assert.IsType<ScopedService>(service);
    }

    [Fact]
    public void Scoped_returns_same_instance_within_scope()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var a = scope.ServiceProvider.GetRequiredService<IScopedService>();
        var b = scope.ServiceProvider.GetRequiredService<IScopedService>();
        Assert.Same(a, b);
    }

    [Fact]
    public void Scoped_returns_different_instance_across_scopes()
    {
        using var provider = BuildProvider();
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();
        var a = scope1.ServiceProvider.GetRequiredService<IScopedService>();
        var b = scope2.ServiceProvider.GetRequiredService<IScopedService>();
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Transient_self_registration_no_interface()
    {
        using var provider = BuildProvider();
        var a = provider.GetService<TransientService>();
        var b = provider.GetService<TransientService>();
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Explicit_as_registers_only_specified_interface()
    {
        using var provider = BuildProvider();
        var service = provider.GetService<IExplicitService>();
        Assert.NotNull(service);
        Assert.IsType<ExplicitService>(service);

        var other = provider.GetService<IOtherService>();
        Assert.Null(other);
    }

    [Fact]
    public void Multi_interface_registers_all_interfaces()
    {
        using var provider = BuildProvider();
        var first = provider.GetService<IFirst>();
        var second = provider.GetService<ISecond>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.IsType<MultiInterfaceService>(first);
        Assert.IsType<MultiInterfaceService>(second);
    }
}
