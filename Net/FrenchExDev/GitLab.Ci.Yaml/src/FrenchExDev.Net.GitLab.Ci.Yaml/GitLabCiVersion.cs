namespace FrenchExDev.Net.GitLab.Ci.Yaml;

public sealed record GitLabCiVersion(int Major, int Minor, int Patch)
    : IComparable<GitLabCiVersion>
{
    public int CompareTo(GitLabCiVersion? other)
    {
        if (other is null) return 1;
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        return c != 0 ? c : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static GitLabCiVersion Parse(string version)
    {
        var v = version.StartsWith('v') ? version[1..] : version;
        var parts = v.Split('.');
        if (parts.Length != 3)
            throw new FormatException($"Invalid version format: '{version}'");
        return new GitLabCiVersion(
            int.Parse(parts[0]),
            int.Parse(parts[1]),
            int.Parse(parts[2]));
    }

    public static bool TryParse(string version, out GitLabCiVersion? result)
    {
        try
        {
            result = Parse(version);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    public static bool operator <(GitLabCiVersion left, GitLabCiVersion right)
        => left.CompareTo(right) < 0;
    public static bool operator >(GitLabCiVersion left, GitLabCiVersion right)
        => left.CompareTo(right) > 0;
    public static bool operator <=(GitLabCiVersion left, GitLabCiVersion right)
        => left.CompareTo(right) <= 0;
    public static bool operator >=(GitLabCiVersion left, GitLabCiVersion right)
        => left.CompareTo(right) >= 0;
}
