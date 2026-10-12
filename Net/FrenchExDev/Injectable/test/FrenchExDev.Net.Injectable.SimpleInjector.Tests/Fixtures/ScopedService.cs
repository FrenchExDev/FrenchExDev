using FrenchExDev.Net.Injectable.Attributes;

namespace FrenchExDev.Net.Injectable.SimpleInjector.Tests.Fixtures;

[Injectable(Scope = Scope.Scoped)]
public class ScopedService : IScopedService
{
    public int Value => 42;
}
