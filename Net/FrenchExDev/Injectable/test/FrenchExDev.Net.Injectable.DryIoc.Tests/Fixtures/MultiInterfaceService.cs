using FrenchExDev.Net.Injectable.Attributes;

namespace FrenchExDev.Net.Injectable.DryIoc.Tests.Fixtures;

public interface IFirst
{
}

public interface ISecond
{
}

[Injectable(Scope = Scope.Scoped)]
public class MultiInterfaceService : IFirst, ISecond
{
}
