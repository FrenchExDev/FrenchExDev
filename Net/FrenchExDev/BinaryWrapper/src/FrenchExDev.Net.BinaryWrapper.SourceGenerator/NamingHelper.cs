using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FrenchExDev.Net.BinaryWrapper.SourceGenerator;

/// <summary>
/// Naming conventions for generated code.
/// </summary>
internal static class NamingHelper
{
    /// <summary>
    /// Converts a kebab-case or snake_case name to PascalCase.
    /// e.g., "parallel-builds" → "ParallelBuilds", "timestamp_ui" → "TimestampUi"
    /// </summary>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        // Strip brackets and parentheses from flag names like [no-]color or replicas)
        name = name.Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "");
        // Strip everything after a space (malformed long names like "s DESCRIPTION")
        var spaceIdx = name.IndexOf(' ');
        if (spaceIdx > 0) name = name.Substring(0, spaceIdx);
        var parts = name.Split(new[] { '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part.Substring(1).ToLowerInvariant());
        }
        return sb.ToString();
    }

    /// <summary>
    /// Maps a CLR type name from JSON to C# type syntax.
    /// </summary>
    public static string MapClrType(string clrType) => clrType switch
    {
        "bool" => "bool",
        "int" => "int",
        "long" => "long",
        "double" => "double",
        "float" => "float",
        "string" => "string",
        _ => "string"
    };

    /// <summary>
    /// Returns the nullable wrapper for a value type.
    /// </summary>
    public static string NullableType(string clrType) => clrType switch
    {
        "bool" or "int" or "long" or "double" or "float" => clrType + "?",
        _ => clrType + "?"
    };

    /// <summary>
    /// Returns true if the type is a value type.
    /// </summary>
    public static bool IsValueType(string clrType) => clrType switch
    {
        "bool" or "int" or "long" or "double" or "float" => true,
        _ => false
    };

    /// <summary>
    /// Generates the command class name from path segments.
    /// e.g., ["packer", "build"] → "PackerBuildCommand"
    /// </summary>
    public static string CommandClassName(string binaryName, UnifiedCommand cmd)
    {
        var sb = new StringBuilder();
        // Skip first segment (binary name) and use PascalCase for each
        foreach (var seg in cmd.PathSegments)
            sb.Append(ToPascalCase(seg));
        sb.Append("Command");
        return sb.ToString();
    }

    /// <summary>
    /// Generates the builder class name.
    /// </summary>
    public static string BuilderClassName(string binaryName, UnifiedCommand cmd)
        => CommandClassName(binaryName, cmd) + "Builder";

    /// <summary>
    /// Generates the client class name.
    /// </summary>
    public static string ClientClassName(string binaryName) => ToPascalCase(binaryName) + "Client";

    /// <summary>
    /// Generates the static entry point class name.
    /// </summary>
    public static string EntryClassName(string binaryName) => ToPascalCase(binaryName);

    /// <summary>
    /// Returns the PascalCase property name for an argument, using DisplayName if set.
    /// </summary>
    public static string ArgumentPropertyName(UnifiedArgument arg) =>
        arg.DisplayName ?? ToPascalCase(arg.Name);

    /// <summary>
    /// Escapes a string for C# string literal.
    /// </summary>
    public static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// Generates a SemanticVersion constructor call from a version string.
    /// </summary>
    public static string SemanticVersionCtor(string version)
    {
        var parts = version.Split('.');
        var major = parts.Length > 0 ? parts[0] : "0";
        var minor = parts.Length > 1 ? parts[1] : "0";
        var patch = parts.Length > 2 ? parts[2] : "0";
        return $"new global::FrenchExDev.Net.BinaryWrapper.SemanticVersion({major}, {minor}, {patch})";
    }

    /// <summary>
    /// Reserved PascalCase names that clash with AbstractBuilder members or C# keywords.
    /// </summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.Ordinal)
    {
        "Reference", "VisitedObjects", "BuildAsync", "Build", "Validate", "ValidateAsync",
        "Instantiate", "CreateInstance", "BuildException"
    };

    /// <summary>
    /// Returns the PascalCase property name for an option, using DisplayName if set.
    /// </summary>
    public static string OptionPropertyName(UnifiedOption opt) =>
        opt.DisplayName ?? ToPascalCase(opt.LongName);

    /// <summary>
    /// Resolves property names for options and arguments, handling clashes:
    /// <list type="bullet">
    /// <item>Options clashing with reserved names → suffixed with "Opt" (e.g., Reference → ReferenceOpt)</item>
    /// <item>Options clashing with arguments → prefixed "Opt" (e.g., File → OptFile), argument → "Arg" prefix (e.g., ArgFile)</item>
    /// <item>Duplicate options/arguments are dropped</item>
    /// </list>
    /// Sets DisplayName on both options and arguments as needed.
    /// </summary>
    public static (List<UnifiedOption> Options, List<UnifiedArgument> Arguments) ResolvePropertyNames(
        List<UnifiedOption> options, List<UnifiedArgument> arguments)
    {
        // Step 1: Deduplicate options
        var dedupedOptions = DeduplicateOptions(options);

        // Step 2: Build option PascalCase name set
        var optNameMap = new Dictionary<string, UnifiedOption>(StringComparer.Ordinal);
        foreach (var opt in dedupedOptions)
        {
            var propName = ToPascalCase(opt.LongName);
            if (!optNameMap.ContainsKey(propName))
                optNameMap[propName] = opt;
        }

        // Step 3: Deduplicate arguments, detect clashes
        var argSeen = new HashSet<string>(StringComparer.Ordinal);
        var dedupedArgs = new List<UnifiedArgument>();
        var clashingNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var arg in arguments)
        {
            var propName = ToPascalCase(arg.Name);
            if (propName.Length == 0 || !char.IsLetter(propName[0]))
                continue;
            if (!argSeen.Add(propName))
                continue; // duplicate argument

            if (optNameMap.ContainsKey(propName))
                clashingNames.Add(propName);

            dedupedArgs.Add(arg);
        }

        // Step 4: Apply renames for clashes
        // Options clashing with arguments: "File" → "OptFile"
        foreach (var opt in dedupedOptions)
        {
            var propName = ToPascalCase(opt.LongName);
            if (clashingNames.Contains(propName))
                opt.DisplayName = "Opt" + propName;
            else if (ReservedNames.Contains(propName))
                opt.DisplayName = propName + "Opt";
        }

        // Arguments clashing with options: "File" → "ArgFile"
        var resultArgs = new List<UnifiedArgument>();
        foreach (var arg in dedupedArgs)
        {
            var propName = ToPascalCase(arg.Name);
            var displayName = propName;

            if (clashingNames.Contains(propName))
                displayName = "Arg" + propName;
            else if (ReservedNames.Contains(propName))
                displayName = propName + "Arg";

            resultArgs.Add(new UnifiedArgument
            {
                Name = arg.Name,
                DisplayName = displayName,
                Position = arg.Position,
                ClrType = arg.ClrType,
                IsRequired = arg.IsRequired,
                IsVariadic = arg.IsVariadic
            });
        }

        return (dedupedOptions, resultArgs);
    }

    /// <summary>
    /// Deduplicates options that map to the same PascalCase property name.
    /// e.g., "--no-tty" and "--[no-]tty" both become "NoTty" — keep only the first.
    /// </summary>
    public static List<UnifiedOption> DeduplicateOptions(List<UnifiedOption> options)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<UnifiedOption>();
        foreach (var opt in options)
        {
            var propName = ToPascalCase(opt.LongName);
            // Skip options that produce invalid C# identifiers (e.g., purely numeric names
            // from help-text wrapping artifacts like "--memory-swap -1").
            if (propName.Length == 0 || !char.IsLetter(propName[0]))
                continue;
            if (seen.Add(propName))
                result.Add(opt);
        }
        return result;
    }
}
