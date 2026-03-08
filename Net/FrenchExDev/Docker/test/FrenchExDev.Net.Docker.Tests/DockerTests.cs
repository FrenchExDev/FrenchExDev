using FrenchExDev.Net.Docker;
using Shouldly;

namespace FrenchExDev.Net.Docker.Tests;

public sealed class DescriptorTests
{
    [Fact]
    public void DockerDescriptor_CanBeInstantiated()
    {
        var descriptor = new DockerDescriptor();
        descriptor.ShouldNotBeNull();
    }
}
