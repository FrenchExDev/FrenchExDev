using Shouldly;

namespace FrenchExDev.Net.DockerCompose.Bundle.Tests;

public class VersioningTests
{
    [Fact]
    public void ComposeSchemaVersions_Available_IsNotEmpty()
    {
        var versions = ComposeSchemaVersions.Available;

        versions.ShouldNotBeEmpty();
        versions.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void ComposeSchemaVersions_Latest_IsValid()
    {
        var latest = ComposeSchemaVersions.Latest;

        latest.ShouldNotBeNullOrWhiteSpace();
        ComposeSchemaVersion.TryParse(latest, out var parsed).ShouldBeTrue();
        parsed.ShouldNotBeNull();
    }

    [Fact]
    public void ComposeSchemaVersions_Oldest_IsValid()
    {
        var oldest = ComposeSchemaVersions.Oldest;

        oldest.ShouldNotBeNullOrWhiteSpace();
        ComposeSchemaVersion.TryParse(oldest, out var parsed).ShouldBeTrue();
        parsed.ShouldNotBeNull();
    }

    [Fact]
    public void ComposeSchemaVersions_Latest_IsGreaterThanOldest()
    {
        var latest = ComposeSchemaVersion.Parse(ComposeSchemaVersions.Latest);
        var oldest = ComposeSchemaVersion.Parse(ComposeSchemaVersions.Oldest);

        (latest > oldest).ShouldBeTrue();
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
    public void ComposeSchemaVersion_Parse_WithoutPrefix_Works()
    {
        var v = ComposeSchemaVersion.Parse("1.5.1");
        v.Major.ShouldBe(1);
        v.Minor.ShouldBe(5);
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

    [Fact]
    public void ComposeSchemaVersion_Equality_Works()
    {
        var v1 = new ComposeSchemaVersion(1, 0, 0);
        var v1Copy = new ComposeSchemaVersion(1, 0, 0);

        v1.ShouldBe(v1Copy);
        (v1 == v1Copy).ShouldBeTrue();
    }

    [Fact]
    public void ComposeSchemaVersion_ToString_FormatsCorrectly()
    {
        var v = new ComposeSchemaVersion(2, 10, 1);
        v.ToString().ShouldBe("2.10.1");
    }

    [Fact]
    public void ComposeSchemaVersion_TryParse_InvalidFormat_ReturnsFalse()
    {
        ComposeSchemaVersion.TryParse("invalid", out var result).ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void ComposeSchemaVersion_Parse_InvalidFormat_Throws()
    {
        Should.Throw<FormatException>(() => ComposeSchemaVersion.Parse("invalid"));
    }

    [Fact]
    public void ComposeSchemaVersions_AllVersions_AreParseable()
    {
        foreach (var version in ComposeSchemaVersions.Available)
        {
            ComposeSchemaVersion.TryParse(version, out var parsed).ShouldBeTrue(
                $"Version '{version}' should be parseable");
            parsed.ShouldNotBeNull();
        }
    }

    [Fact]
    public void ComposeSchemaVersions_AllVersions_AreInAscendingOrder()
    {
        var versions = ComposeSchemaVersions.Available;

        for (var i = 1; i < versions.Count; i++)
        {
            var prev = ComposeSchemaVersion.Parse(versions[i - 1]);
            var curr = ComposeSchemaVersion.Parse(versions[i]);
            (prev < curr).ShouldBeTrue(
                $"Version {versions[i - 1]} should be less than {versions[i]}");
        }
    }

    [Fact]
    public void ComposeSchemaVersion_CompareTo_NullReturnsPositive()
    {
        var v = new ComposeSchemaVersion(1, 0, 0);
        v.CompareTo(null).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ComposeSchemaVersion_LessThanOrEqual_Works()
    {
        var v1 = new ComposeSchemaVersion(1, 0, 0);
        var v1Copy = new ComposeSchemaVersion(1, 0, 0);
        var v2 = new ComposeSchemaVersion(2, 0, 0);

        (v1 <= v1Copy).ShouldBeTrue();
        (v1 <= v2).ShouldBeTrue();
        (v2 <= v1).ShouldBeFalse();
    }

    [Fact]
    public void ComposeSchemaVersion_GreaterThanOrEqual_Works()
    {
        var v1 = new ComposeSchemaVersion(1, 0, 0);
        var v1Copy = new ComposeSchemaVersion(1, 0, 0);
        var v2 = new ComposeSchemaVersion(2, 0, 0);

        (v1 >= v1Copy).ShouldBeTrue();
        (v2 >= v1).ShouldBeTrue();
        (v1 >= v2).ShouldBeFalse();
    }
}
