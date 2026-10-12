using FrenchExDev.Net.Injectable.Attributes;

namespace FrenchExDev.Net.Injectable.Microsoft.Tests.Fixtures;

[Injectable(Scope = Scope.Singleton)]
public class SingletonService : ISingletonService
{
    public string Name => nameof(SingletonService);
}
