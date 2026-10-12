namespace FrenchExDev.Net.BinaryWrapper.Design.Lib;

public static class DotEnvLoader
{
    public static Dictionary<string, string> Load(string? startDir = null)
    {
        var dir = startDir ?? Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            var envFile = Path.Combine(dir, ".env");
            if (File.Exists(envFile))
                return ParseEnvFile(envFile);
            dir = Path.GetDirectoryName(dir);
        }
        return [];
    }

    private static Dictionary<string, string> ParseEnvFile(string path)
    {
        var result = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;
            var eq = trimmed.IndexOf('=');
            if (eq <= 0) continue;
            result[trimmed[..eq].Trim()] = trimmed[(eq + 1)..].Trim();
        }
        return result;
    }
}
