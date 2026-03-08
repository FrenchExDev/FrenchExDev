using FrenchExDev.Net.DockerCompose;
using Shouldly;

namespace FrenchExDev.Net.DockerCompose.Tests;

public sealed class DescriptorTests
{
    [Fact]
    public void DockerComposeDescriptor_CanBeInstantiated()
    {
        var descriptor = new DockerComposeDescriptor();
        descriptor.ShouldNotBeNull();
    }
}
