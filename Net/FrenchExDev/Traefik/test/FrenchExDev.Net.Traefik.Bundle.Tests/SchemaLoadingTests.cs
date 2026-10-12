using System.Reflection;
using Shouldly;

namespace FrenchExDev.Net.Traefik.Bundle.Tests;

public class SchemaLoadingTests
{
    [Fact]
    public void Schemas_AreEmbeddedResources()
    {
        var assembly = typeof(TraefikBundleDescriptor).Assembly;
        var names = assembly.GetManifestResourceNames();
        names.ShouldContain(n => n.Contains("traefik-v3-static"));
        names.ShouldContain(n => n.Contains("traefik-v3-file-provider"));
    }

    [Fact]
    public void StaticSchema_CanBeRead()
    {
        var assembly = typeof(TraefikBundleDescriptor).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.Contains("traefik-v3-static"));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        content.ShouldNotBeNullOrEmpty();
        content.ShouldContain("\"$defs\"");
    }

    [Fact]
    public void DynamicSchema_CanBeRead()
    {
        var assembly = typeof(TraefikBundleDescriptor).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.Contains("traefik-v3-file-provider"));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        content.ShouldNotBeNullOrEmpty();
        content.ShouldContain("\"definitions\"");
    }
}
