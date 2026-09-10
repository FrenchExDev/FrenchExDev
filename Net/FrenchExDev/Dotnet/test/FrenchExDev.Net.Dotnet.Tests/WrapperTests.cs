using System.Reflection;
using FrenchExDev.Net.BinaryWrapper;
using FrenchExDev.Net.BinaryWrapper.Attributes;
using Shouldly;

namespace FrenchExDev.Net.Dotnet.Tests;

public sealed class WrapperTests
{
    [Fact]
    public void Descriptor_UsesExpectedExecutableAndSerialization()
    {
        var descriptor = typeof(DotnetDescriptor).GetCustomAttribute<BinaryWrapperAttribute>();
        descriptor.ShouldNotBeNull();
        descriptor.BinaryName.ShouldBe("dotnet");
        descriptor.FlagPrefix.ShouldBe("--");
        descriptor.FlagValueSeparator.ShouldBe(" ");
        descriptor.UseBoolEqualsFormat.ShouldBe(false);
    }

    [Fact]
    public void GeneratedClient_CanBeCreatedWithoutAnInstalledBinary()
    {
        var binding = new BinaryBinding
        {
            Identifier = new BinaryIdentifier("dotnet"),
            ExecutablePath = "dotnet",
        };
        var client = global::FrenchExDev.Net.Dotnet.Dotnet.Create(binding);
        client.ShouldBeOfType<global::FrenchExDev.Net.Dotnet.DotnetClient>();
    }
}
