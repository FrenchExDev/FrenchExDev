using System.Diagnostics.CodeAnalysis;

namespace FrenchExDev.Net.QualityGate.Reports;

/// <summary>
/// Resolves glob patterns to matching file paths under a base directory.
/// Supports <c>**</c> for recursive search and <c>*</c> wildcards in the filename portion.
/// </summary>
internal static class GlobResolver
{
    public static string[] Resolve(string baseDir, string glob)
    {
        if (!Directory.Exists(baseDir))
            return [];

        var normalized = glob.Replace('\\', '/');
        var segments = normalized.Split('/');
        var filePattern = segments[^1];

        var searchOption = DetermineSearchOption(segments);
        var subDir = WalkSubDirectories(baseDir, segments, ref searchOption);

        return GetFilesSafe(subDir, filePattern, searchOption);
    }

    [ExcludeFromCodeCoverage] // Race condition guard: directory deleted between check and search
    private static string[] GetFilesSafe(string subDir, string filePattern, SearchOption searchOption)
    {
        try
        {
            return Directory.GetFiles(subDir, filePattern, searchOption);
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static SearchOption DetermineSearchOption(string[] segments)
    {
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i] == "**")
                return SearchOption.AllDirectories;
        }
        return SearchOption.TopDirectoryOnly;
    }

    private static string WalkSubDirectories(string baseDir, string[] segments, ref SearchOption searchOption)
    {
        var subDir = baseDir;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i] == "**")
            {
                searchOption = SearchOption.AllDirectories;
                continue;
            }

            var candidate = Path.Combine(subDir, segments[i]);
            if (Directory.Exists(candidate))
                subDir = candidate;
        }
        return subDir;
    }
}
