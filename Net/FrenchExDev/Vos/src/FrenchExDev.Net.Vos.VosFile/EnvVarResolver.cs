using System.Text.RegularExpressions;

namespace FrenchExDev.Net.Vos.VosFile;

/// <summary>
/// Resolves ${VAR_NAME} patterns in VosConfig string values from environment variables.
/// </summary>
public static partial class EnvVarResolver
{
    [GeneratedRegex(@"\$\{([^}]+)\}", RegexOptions.Compiled)]
    private static partial Regex EnvVarPattern();

    public static VosConfig Resolve(VosConfig config)
    {
        // For now, resolve env vars in machine type variables and provisioning env
        foreach (var (_, machineType) in config.MachineTypes)
        {
            // Variables
            var resolvedVars = new Dictionary<string, string>();
            foreach (var (key, value) in machineType.Variables)
                resolvedVars[key] = ResolveString(value);
            machineType.Variables = resolvedVars;

            // Provisioning env
            foreach (var step in machineType.Provisioning)
            {
                var resolvedEnv = new Dictionary<string, string>();
                foreach (var (key, value) in step.Env)
                    resolvedEnv[key] = ResolveString(value);
                step.Env = resolvedEnv;
            }
        }

        return config;
    }

    private static string ResolveString(string input)
    {
        return EnvVarPattern().Replace(input, match =>
        {
            var varName = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(varName) ?? match.Value;
        });
    }
}
