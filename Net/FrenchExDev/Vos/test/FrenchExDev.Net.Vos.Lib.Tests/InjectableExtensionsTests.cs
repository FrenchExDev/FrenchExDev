using Microsoft.Extensions.DependencyInjection;

namespace FrenchExDev.Net.Vos.Lib.Tests;

public class InjectableExtensionsTests
{
    [Fact]
    public void AddFrenchExDevNetVosLibInjectables_registers_all_services()
    {
        var services = new ServiceCollection();
        services.AddFrenchExDevNetVosLibInjectables();

        services.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void AddFrenchExDevNetVosLibInjectables_registers_event_emitter_as_singleton()
    {
        var services = new ServiceCollection();
        services.AddFrenchExDevNetVosLibInjectables();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IVosEventEmitter));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddFrenchExDevNetVosLibInjectables_registers_services_as_transient()
    {
        var services = new ServiceCollection();
        services.AddFrenchExDevNetVosLibInjectables();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IVosProjectService));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Transient);
    }
}
