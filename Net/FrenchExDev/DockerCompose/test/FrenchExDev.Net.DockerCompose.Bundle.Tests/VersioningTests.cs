using FrenchExDev.Net.DockerCompose.Bundle.Versioning;
using Shouldly;

namespace FrenchExDev.Net.DockerCompose.Bundle.Tests;

public class VersioningTests
{
    [Fact]
    public void SchemaDiffer_Compare_DetectsAddedProperties()
    {
        var versions = ComposeSchema.GetAvailableVersions();
        if (versions.Count < 2) return;

        var diff = SchemaDiffer.Compare(versions[0], versions[^1]);

        diff.From.ShouldBe(versions[0]);
        diff.To.ShouldBe(versions[^1]);
        diff.AddedProperties.ShouldNotBeEmpty();
    }

    [Fact]
    public void SchemaVersionDetector_DetectsDeprecation_ForVersionField()
    {
        var file = new Model.ComposeFile { Version = "3.8" };
        file.Services["web"] = new Model.Service { Image = "nginx" };

        var warnings = SchemaVersionDetector.GetDeprecationWarnings(file);

        warnings.ShouldNotBeEmpty();
        warnings.ShouldContain(w => w.Field == "version");
    }

    [Fact]
    public void ComposeSchemaVersion_Parse_Works()
    {
        var v = ComposeSchemaVersion.Parse("v2.10.1");
        v.Major.ShouldBe(2);
        v.Minor.ShouldBe(10);
        v.Patch.ShouldBe(1);
    }

    [Fact]
    public void ComposeSchemaVersion_Comparison_Works()
    {
        var v1 = new ComposeSchemaVersion(1, 0, 0);
        var v2 = new ComposeSchemaVersion(2, 0, 0);
        var v1_1 = new ComposeSchemaVersion(1, 1, 0);

        (v1 < v2).ShouldBeTrue();
        (v1 < v1_1).ShouldBeTrue();
        (v2 > v1_1).ShouldBeTrue();
    }
}
