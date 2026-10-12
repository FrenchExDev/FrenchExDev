using FrenchExDev.Net.PodmanCompose;
using Shouldly;

namespace FrenchExDev.Net.PodmanCompose.Tests;

public sealed class DescriptorTests
{
    [Fact]
    public void PodmanComposeDescriptor_CanBeInstantiated()
    {
        var descriptor = new PodmanComposeDescriptor();
        descriptor.ShouldNotBeNull();
    }
}
