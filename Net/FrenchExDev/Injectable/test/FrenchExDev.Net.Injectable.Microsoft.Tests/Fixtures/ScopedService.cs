using FrenchExDev.Net.Injectable.Attributes;

namespace FrenchExDev.Net.Injectable.Microsoft.Tests.Fixtures;

[Injectable(Scope = Scope.Scoped)]
public class ScopedService : IScopedService
{
    public int Value => 42;
}
