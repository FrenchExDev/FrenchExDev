using System.Reflection;
using Shouldly;

namespace FrenchExDev.Net.DockerCompose.Bundle.Tests;

public class SchemaLoadingTests
{
    [Fact]
    public void EmbeddedSchemas_Exist_ForAllVersions()
    {
        var assembly = typeof(ComposeFile).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        resourceNames.ShouldNotBeEmpty();

        var schemaResources = resourceNames
            .Where(n => n.Contains("compose-spec") && n.EndsWith(".json"))
            .ToList();

        schemaResources.ShouldNotBeEmpty();
    }

    [Fact]
    public void EmbeddedSchema_CanBeLoaded()
    {
        var assembly = typeof(ComposeFile).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        var schemaResource = resourceNames
            .FirstOrDefault(n => n.Contains("compose-spec") && n.EndsWith(".json"));

        schemaResource.ShouldNotBeNull();

        using var stream = assembly.GetManifestResourceStream(schemaResource);
        stream.ShouldNotBeNull();
        stream!.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ComposeSchemaVersions_Available_ContainsLatest()
    {
        ComposeSchemaVersions.Available.ShouldContain(ComposeSchemaVersions.Latest);
    }

    [Fact]
    public void ComposeSchemaVersions_Available_ContainsOldest()
    {
        ComposeSchemaVersions.Available.ShouldContain(ComposeSchemaVersions.Oldest);
    }

    [Fact]
    public void ComposeSchemaVersions_Latest_IsLastInAvailable()
    {
        ComposeSchemaVersions.Available[^1].ShouldBe(ComposeSchemaVersions.Latest);
    }

    [Fact]
    public void ComposeSchemaVersions_Oldest_IsFirstInAvailable()
    {
        ComposeSchemaVersions.Available[0].ShouldBe(ComposeSchemaVersions.Oldest);
    }
}
