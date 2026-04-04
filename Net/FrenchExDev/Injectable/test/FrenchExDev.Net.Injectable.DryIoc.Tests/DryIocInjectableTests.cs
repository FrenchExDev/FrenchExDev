using DryIoc;
using FrenchExDev.Net.Injectable.DryIoc.Tests.Fixtures;
using Xunit;

namespace FrenchExDev.Net.Injectable.DryIoc.Tests;

public class DryIocInjectableTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        container.AddFrenchExDevNetInjectableDryIocTestsInjectables();
        return container;
    }

    [Fact]
    public void Singleton_is_registered_as_interface()
    {
        using var container = BuildContainer();
        var service = container.Resolve<ISingletonService>();
        Assert.NotNull(service);
        Assert.IsType<SingletonService>(service);
    }

    [Fact]
    public void Singleton_returns_same_instance()
    {
        using var container = BuildContainer();
        var a = container.Resolve<ISingletonService>();
        var b = container.Resolve<ISingletonService>();
        Assert.Same(a, b);
    }

    [Fact]
    public void Scoped_is_registered_as_interface()
    {
        using var container = BuildContainer();
        using var scope = container.OpenScope();
        var service = scope.Resolve<IScopedService>();
        Assert.NotNull(service);
        Assert.IsType<ScopedService>(service);
    }

    [Fact]
    public void Scoped_returns_same_instance_within_scope()
    {
        using var container = BuildContainer();
        using var scope = container.OpenScope();
        var a = scope.Resolve<IScopedService>();
        var b = scope.Resolve<IScopedService>();
        Assert.Same(a, b);
    }

    [Fact]
    public void Scoped_returns_different_instance_across_scopes()
    {
        using var container = BuildContainer();
        using var scope1 = container.OpenScope();
        using var scope2 = container.OpenScope();
        var a = scope1.Resolve<IScopedService>();
        var b = scope2.Resolve<IScopedService>();
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Transient_self_registration_no_interface()
    {
        using var container = BuildContainer();
        var a = container.Resolve<TransientService>();
        var b = container.Resolve<TransientService>();
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Explicit_as_registers_only_specified_interface()
    {
        using var container = BuildContainer();
        var service = container.Resolve<IExplicitService>();
        Assert.NotNull(service);
        Assert.IsType<ExplicitService>(service);

        Assert.Throws<ContainerException>(() => container.Resolve<IOtherService>());
    }

    [Fact]
    public void Multi_interface_registers_all_interfaces()
    {
        using var container = BuildContainer();
        using var scope = container.OpenScope();
        var first = scope.Resolve<IFirst>();
        var second = scope.Resolve<ISecond>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.IsType<MultiInterfaceService>(first);
        Assert.IsType<MultiInterfaceService>(second);
    }
}
