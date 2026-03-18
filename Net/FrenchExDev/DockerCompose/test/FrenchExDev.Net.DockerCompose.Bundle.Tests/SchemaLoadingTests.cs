using FrenchExDev.Net.DockerCompose.Bundle;
using Shouldly;

namespace FrenchExDev.Net.DockerCompose.Bundle.Tests;

public class SchemaLoadingTests
{
    [Fact]
    public void GetAvailableVersions_ReturnsNonEmptyList()
    {
        var versions = ComposeSchema.GetAvailableVersions();
        versions.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetAvailableVersions_IsSorted()
    {
        var versions = ComposeSchema.GetAvailableVersions();
        for (int i = 1; i < versions.Count; i++)
            versions[i].ShouldBeGreaterThan(versions[i - 1]);
    }

    [Fact]
    public void LatestVersion_IsLastInList()
    {
        var versions = ComposeSchema.GetAvailableVersions();
        ComposeSchema.LatestVersion.ShouldBe(versions[^1]);
    }

    [Fact]
    public void GetSchema_ReturnsValidJson_ForEachVersion()
    {
        foreach (var version in ComposeSchema.GetAvailableVersions())
        {
            var schema = ComposeSchema.GetSchema(version);
            schema.ShouldNotBeNull();
            schema.RootElement.GetProperty("$schema").GetString()!
                .ShouldContain("json-schema.org");
        }
    }

    [Fact]
    public void GetSchema_ThrowsForUnknownVersion()
    {
        Should.Throw<ArgumentException>(() =>
            ComposeSchema.GetSchema(new ComposeSchemaVersion(99, 99, 99)));
    }

    [Fact]
    public void GetLatestSchema_HasServicesDefinition()
    {
        var schema = ComposeSchema.GetLatestSchema();
        schema.RootElement.TryGetProperty("properties", out var props).ShouldBeTrue();
        props.TryGetProperty("services", out _).ShouldBeTrue();
    }
}
