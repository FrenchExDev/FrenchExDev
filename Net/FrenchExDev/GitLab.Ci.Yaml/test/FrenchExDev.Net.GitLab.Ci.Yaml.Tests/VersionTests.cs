using Shouldly;

namespace FrenchExDev.Net.GitLab.Ci.Yaml.Tests;

public class VersionTests
{
    [Fact]
    public void Parse_valid_version()
    {
        var v = GitLabCiVersion.Parse("17.6.4");
        v.Major.ShouldBe(17);
        v.Minor.ShouldBe(6);
        v.Patch.ShouldBe(4);
    }

    [Fact]
    public void Parse_with_v_prefix()
    {
        var v = GitLabCiVersion.Parse("v17.6.4");
        v.Major.ShouldBe(17);
    }

    [Fact]
    public void CompareTo_returns_correct_order()
    {
        var v1 = GitLabCiVersion.Parse("16.0.0");
        var v2 = GitLabCiVersion.Parse("17.6.4");
        (v1 < v2).ShouldBeTrue();
        (v2 > v1).ShouldBeTrue();
    }

    [Fact]
    public void ToString_formats_correctly()
    {
        var v = new GitLabCiVersion(17, 6, 4);
        v.ToString().ShouldBe("17.6.4");
    }

    [Fact]
    public void TryParse_returns_false_for_invalid()
    {
        GitLabCiVersion.TryParse("invalid", out var result).ShouldBeFalse();
        result.ShouldBeNull();
    }
}
