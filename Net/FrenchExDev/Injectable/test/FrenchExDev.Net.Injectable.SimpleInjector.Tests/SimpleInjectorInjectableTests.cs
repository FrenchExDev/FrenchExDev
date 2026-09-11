using FrenchExDev.Net.Injectable.SimpleInjector.Tests.Fixtures;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Xunit;

namespace FrenchExDev.Net.Injectable.SimpleInjector.Tests;

public class SimpleInjectorInjectableTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        container.AddFrenchExDevNetInjectableSimpleInjectorTestsInjectables();
        container.Verify();
        return container;
    }

    [Fact]
    public void Singleton_is_registered_as_interface()
    {
        using var container = BuildContainer();
        var service = container.GetInstance<ISingletonService>();
        Assert.NotNull(service);
        Assert.IsType<SingletonService>(service);
    }

    [Fact]
    public void Singleton_returns_same_instance()
    {
        using var container = BuildContainer();
        var a = container.GetInstance<ISingletonService>();
        var b = container.GetInstance<ISingletonService>();
        Assert.Same(a, b);
    }

    [Fact]
    public void Scoped_is_registered_as_interface()
    {
        using var container = BuildContainer();
        using (AsyncScopedLifestyle.BeginScope(container))
        {
            var service = container.GetInstance<IScopedService>();
            Assert.NotNull(service);
            Assert.IsType<ScopedService>(service);
        }
    }

    [Fact]
    public void Scoped_returns_same_instance_within_scope()
    {
        using var container = BuildContainer();
        using (AsyncScopedLifestyle.BeginScope(container))
        {
            var a = container.GetInstance<IScopedService>();
            var b = container.GetInstance<IScopedService>();
            Assert.Same(a, b);
        }
    }

    [Fact]
    public void Transient_self_registration()
    {
        using var container = BuildContainer();
        var a = container.GetInstance<TransientService>();
        var b = container.GetInstance<TransientService>();
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Explicit_as_registers_only_specified_interface()
    {
        using var container = BuildContainer();
        var service = container.GetInstance<IExplicitService>();
        Assert.NotNull(service);
        Assert.IsType<ExplicitService>(service);
    }

    [Fact]
    public void Multi_interface_registers_all_interfaces()
    {
        using var container = BuildContainer();
        using (AsyncScopedLifestyle.BeginScope(container))
        {
            var first = container.GetInstance<IFirst>();
            var second = container.GetInstance<ISecond>();
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.IsType<MultiInterfaceService>(first);
            Assert.IsType<MultiInterfaceService>(second);
        }
    }
}
