using FrenchExDev.Net.Injectable.Attributes;

namespace FrenchExDev.Net.Injectable.DryIoc.Tests.Fixtures;

[Injectable(Scope = Scope.Singleton, As = new[] { typeof(IExplicitService) })]
public class ExplicitService : IExplicitService, IOtherService
{
}
