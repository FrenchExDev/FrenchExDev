using System.Text.RegularExpressions;
using FrenchExDev.Net.BinaryWrapper.Design;

namespace FrenchExDev.Net.Dotnet.Design;

/// <summary>
/// Parses SDK root help and System.CommandLine/NuGet command help, including
/// English/French headings, aliases, wrapped descriptions and usage arity.
/// </summary>
public sealed class DotnetHelpParser : IHelpParser
{
    private enum Section { None, Description, Usage, Commands, Options, Arguments }

    public CommandNode? Parse(string helpText, string commandName)
    {
        if (string.IsNullOrWhiteSpace(helpText)) return null;
        var lines = helpText.Replace('\u00a0', ' ').Replace("\r", "").Split('\n');

        // These bundled tools have their own option grammars (colon values,
        // slash prefixes, F# compiler switches). Preserve exact tokens instead
        // of pretending they are GNU options.
        if (commandName is "msbuild" or "vstest" or "fsi")
            return new CommandNode
            {
                Name = commandName,
                Description = "Pass-through arguments for the bundled tool; preserve its native switch syntax.",
                Arguments = [new ArgumentDefinition { Name = "arguments", IsRequired = false, IsVariadic = true }],
            };

        var builder = new CommandNodeBuilder(commandName);
        var section = Section.None;
        int? entryIndent = null;
        var usage = new List<string>();
        var isNuget = Regex.IsMatch(helpText, @"dotnet nuget(?:\s|$)");
        var isWatch = commandName == "watch";
        var isUserSecrets = helpText.Contains("dotnet user-secrets", StringComparison.Ordinal);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            var indent = line.Length - line.TrimStart().Length;
            if (TryHeader(trimmed, out var header))
            {
                section = header;
                entryIndent = null;
                // Root help puts its usage on the heading's line.
                if (header == Section.Usage)
                {
                    var rest = trimmed[(trimmed.IndexOf(':') + 1)..].Trim();
                    if (Regex.IsMatch(rest, @"^dotnet(?:\s|-)")) usage.Add(rest);
                }
                continue;
            }
            if (indent == 0)
            {
                section = Section.None;
                continue;
            }
            if (section == Section.Description)
            {
                builder.Description = Append(builder.Description, trimmed);
                continue;
            }
            if (section == Section.Usage)
            {
                if (Regex.IsMatch(trimmed, @"^dotnet(?:\s|-)")) usage.Add(trimmed);
                continue;
            }
            if (section is not (Section.Commands or Section.Options or Section.Arguments)) continue;

            entryIndent ??= indent;
            if (indent > entryIndent)
            {
                if (section == Section.Options && builder.Options.Count > 0)
                    builder.Options[^1] = builder.Options[^1] with { Description = Append(builder.Options[^1].Description, trimmed) };
                else if (section == Section.Arguments && builder.Arguments.Count > 0)
                    builder.Arguments[^1] = builder.Arguments[^1] with { Description = Append(builder.Arguments[^1].Description, trimmed) };
                continue;
            }

            var columns = Regex.Split(trimmed, @"\s{2,}|\t+", RegexOptions.None, TimeSpan.FromSeconds(1));
            var signature = columns[0];
            var description = columns.Length > 1 ? string.Join(" ", columns.Skip(1)) : null;
            switch (section)
            {
                case Section.Commands:
                    var command = Regex.Match(signature, @"^[a-z][a-z0-9-]*");
                    // dotnet help opens documentation; do not scrape it recursively.
                    if (command.Success && command.Value != "help" && command.Value != commandName
                        && builder.SubCommands.All(c => c.Name != command.Value))
                        builder.SubCommands.Add(new CommandNode { Name = command.Value, Description = description });
                    break;
                case Section.Options:
                    ParseOption(signature, description, builder, isNuget, isWatch, isUserSecrets);
                    break;
                case Section.Arguments:
                    ParseArgument(signature, description, usage, builder);
                    break;
            }
        }
        for (var i = 0; i < builder.Options.Count; i++)
        {
            var option = builder.Options[i];
            if (option.ValueKind == OptionValueKind.Single
                && option.Description?.Contains("multiple times", StringComparison.OrdinalIgnoreCase) == true)
                builder.Options[i] = option with { ValueKind = OptionValueKind.Multiple };
        }
        return builder.Build();
    }

    private static bool TryHeader(string line, out Section section)
    {
        section = Section.None;
        var colon = line.IndexOf(':');
        if (colon < 0) return false;
        var name = line[..colon].Trim().ToLowerInvariant();
        section = name switch
        {
            "description" => Section.Description,
            "usage" or "utilisation" => Section.Usage,
            "commands" or "available commands" or "sdk commands" or "additional commands from bundled tools"
                or "commandes" or "commandes disponibles" or "commandes du sdk"
                or "commandes supplémentaires d'outils groupés" => Section.Commands,
            "options" or "sdk-options" or "runtime-options" or "options cli .net" or "options .net cli" => Section.Options,
            "arguments" => Section.Arguments,
            _ => Section.None,
        };
        // Other unindented headings end the current section too.
        return section != Section.None;
    }

    private static void ParseOption(string signature, string? description, CommandNodeBuilder builder, bool isNuget, bool isWatch, bool isUserSecrets)
    {
        if (!signature.StartsWith('-')) return;
        var aliases = Regex.Matches(signature, @"(?<![\w-])--?[A-Za-z?][A-Za-z0-9-]*")
            .Select(m => m.Value).Distinct().ToList();
        if (aliases.Count == 0) return;
        var canonical = aliases.Where(a => a.StartsWith("--", StringComparison.Ordinal)).OrderByDescending(a => a.Length).FirstOrDefault()
            ?? aliases.OrderByDescending(a => a.Length).First();
        if (canonical == "-?") return;
        var name = canonical.TrimStart('-');
        if (builder.Options.Any(o => o.LongName == name)) return;
        // Older NuGet/watch help omits placeholders even for value options.
        var hasValue = signature.Contains('<')
            || (isNuget && NugetValueOptions.Contains(name))
            || (isWatch && name is "project" or "launch-profile" or "framework" or "property")
            || (isUserSecrets && name == "id");
        var multiple = hasValue && (signature.Contains("...", StringComparison.Ordinal)
            || description?.Contains("multiple times", StringComparison.OrdinalIgnoreCase) == true);
        builder.Options.Add(new OptionDefinition
        {
            LongName = name,
            ShortName = aliases.FirstOrDefault(a => a.Length == 2 && a != "-?")?.TrimStart('-'),
            Description = description,
            ValueKind = hasValue ? multiple ? OptionValueKind.Multiple : OptionValueKind.Single : OptionValueKind.Flag,
            ClrType = hasValue ? "string" : "bool",
            IsRequired = signature.Contains("(REQUIRED)", StringComparison.OrdinalIgnoreCase),
        });
    }

    private static void ParseArgument(string signature, string? description, List<string> usage, CommandNodeBuilder builder)
    {
        var match = Regex.Match(signature, @"^(?:<(?<name>[^>]+)>|\[(?<name>[^\]]+)\]|(?<name>[A-Za-z][A-Za-z0-9_-]*))");
        if (!match.Success) return;
        var placeholder = match.Value;
        var occurrences = usage.SelectMany(line => Regex.Matches(line, Regex.Escape(placeholder))
            .Select(m => (Line: line, Match: m))).ToList();
        // BinaryWrapper appends arguments after the command path. Parent arguments
        // such as `sln <SLN_FILE> add` cannot be represented in that layout.
        if (occurrences.Count > 0 && occurrences.All(o => o.Match.Index < CommandIndex(o.Line, builder.Name))) return;
        var optional = occurrences.Count > 0
            ? occurrences.All(o => BracketDepth(o.Line.AsSpan(0, o.Match.Index)) > 0)
            : signature.StartsWith('[');
        var variadic = signature == "[root]" || signature.Contains("...", StringComparison.Ordinal) || occurrences.Any(o =>
            o.Line[(o.Match.Index + o.Match.Length)..].TrimStart(']').StartsWith("...", StringComparison.Ordinal));
        var name = Regex.Replace(match.Groups["name"].Value, @"\s*\|\s*", "-or-");
        name = Regex.Replace(name, @"([a-z0-9])([A-Z])", "$1-$2");
        name = Regex.Replace(name, @"[^A-Za-z0-9_-]+", "-").Trim('-').ToLowerInvariant();
        if (name.Length == 0 || builder.Arguments.Any(a => a.Name == name)) return;
        builder.Arguments.Add(new ArgumentDefinition
        {
            Name = name,
            Position = builder.Arguments.Count,
            Description = description,
            IsRequired = !optional,
            IsVariadic = variadic,
        });
    }

    private static int CommandIndex(string line, string commandName) =>
        Regex.Matches(line, @"(?<!\S)" + Regex.Escape(commandName) + @"(?!\S)").LastOrDefault()?.Index ?? 0;

    private static int BracketDepth(ReadOnlySpan<char> prefix)
    {
        var depth = 0;
        foreach (var character in prefix)
        {
            if (character == '[') depth++;
            else if (character == ']') depth--;
        }
        return depth;
    }

    // NuGet.CommandLine.XPlat's legacy help does not print value placeholders.
    // --version at the nuget root is a flag, so it is deliberately absent here.
    private static readonly HashSet<string> NugetValueOptions = new(StringComparer.Ordinal)
    {
        "name", "username", "password", "source", "package-source", "output",
        "configfile", "valid-authentication-types", "protocol-version", "verbosity",
        "certificate-fingerprint", "certificate-password", "certificate-path",
        "certificate-store-location", "certificate-store-name", "certificate-subject-name",
        "find-by", "find-value", "format", "hash-algorithm", "path", "store-location",
        "store-name", "timestamp-hash-algorithm", "timestamper",
    };

    private static string Append(string? description, string text) =>
        description is null ? text : description + " " + text;
}
