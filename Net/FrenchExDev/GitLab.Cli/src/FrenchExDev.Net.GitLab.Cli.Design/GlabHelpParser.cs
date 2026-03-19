using System.Text.RegularExpressions;
using FrenchExDev.Net.BinaryWrapper.Design;

namespace FrenchExDev.Net.GitLab.Cli.Design;

/// <summary>
/// Parser for glab's custom help template. glab uses Cobra internally but applies
/// a heavily customized output format that evolved across versions:
/// <list type="bullet">
///   <item>Older versions (≤~1.80): <c>CORE COMMANDS</c>, <c>name:</c> colon format,
///     <c>-v, --version</c> (comma-separated flags)</item>
///   <item>Newer versions (≥~1.81): <c>COMMANDS</c>, <c>name [args]</c> format,
///     <c>-v --version</c> (space-separated, no comma)</item>
/// </list>
/// Both eras use ALL-CAPS section headers and heuristic value detection.
/// </summary>
public sealed class GlabHelpParser : IHelpParser
{
    private enum Section { None, Usage, Commands, Flags, Examples, Aliases }

    private static readonly HashSet<string> SkippedCommands =
        new(["help", "completion", "check-update"], StringComparer.OrdinalIgnoreCase);

    // Matches a trailing default value like "(created_at)", "(text)", "(30)"
    private static readonly Regex DefaultValuePattern = new(
        @"\(([^()]+)\)\s*$", RegexOptions.Compiled);

    // Matches a placeholder like <username>, <field>, <id>, <string>
    private static readonly Regex PlaceholderPattern = new(
        @"<[a-zA-Z][a-zA-Z0-9_-]*>", RegexOptions.Compiled);

    // Matches phrases indicating repeated/multiple values
    private static readonly Regex MultipleValuePattern = new(
        @"comma[- ]separated|repeating the flag|multiple\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public CommandNode? Parse(string helpText, string commandName)
    {
        if (string.IsNullOrWhiteSpace(helpText))
            return null;

        var builder = new CommandNodeBuilder(commandName);
        var lines = helpText.Split('\n');
        var section = Section.None;
        string? description = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // Detect ALL-CAPS section headers
            if (IsSectionHeader(trimmed, out var newSection))
            {
                section = newSection;
                continue;
            }

            // Content lines must be indented (glab uses leading spaces for all content)
            if (section != Section.None && !line.StartsWith(' ') && !line.StartsWith('\t'))
            {
                section = Section.None;
                // Fall through — might be a description line
            }

            if (section == Section.None)
            {
                // First non-empty, non-header line is the description
                description ??= trimmed;
                continue;
            }

            switch (section)
            {
                case Section.Commands:
                    ParseCommandLine(trimmed, builder);
                    break;
                case Section.Flags:
                    ParseFlagLine(trimmed, builder);
                    break;
                // Usage, Examples, Aliases — skip content
            }
        }

        builder.Description = description;
        return builder.Build();
    }

    private static bool IsSectionHeader(string trimmed, out Section section)
    {
        section = Section.None;

        // glab uses ALL-CAPS headers. The header name varies across versions:
        //   "CORE COMMANDS", "COMMANDS", "OTHER COMMANDS", "ADDITIONAL COMMANDS"
        if (trimmed.EndsWith("COMMANDS") && trimmed == trimmed.ToUpperInvariant())
        { section = Section.Commands; return true; }

        if (trimmed is "FLAGS" or "INHERITED FLAGS")
        { section = Section.Flags; return true; }

        if (trimmed is "USAGE")
        { section = Section.Usage; return true; }

        if (trimmed is "EXAMPLES")
        { section = Section.Examples; return true; }

        if (trimmed is "ALIASES")
        { section = Section.Aliases; return true; }

        // Skip known non-command/non-flag sections
        if (trimmed is "ENVIRONMENT VARIABLES" or "LEARN MORE" or "FEEDBACK")
        { section = Section.None; return true; }

        return false;
    }

    private static void ParseCommandLine(string line, CommandNodeBuilder builder)
    {
        // Two formats across versions:
        //   Older: "alias:       Create, list, and delete aliases."
        //   Newer: "alias [command] [--flags]   Create, list, and delete aliases."
        // Split on 2+ spaces to separate command from description
        var parts = Regex.Split(line, @"\s{2,}");
        if (parts.Length == 0) return;

        // Must have a description part — lines without one are continuations/examples
        // (e.g. "glab repo clone -g <group>" appearing under the clone entry)
        if (parts.Length < 2) return;

        // The command portion may include argument hints or trailing colon
        var commandPart = parts[0].Trim();
        var commandName = commandPart.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

        // Strip trailing colon (older format: "alias:")
        commandName = commandName.TrimEnd(':');

        if (string.IsNullOrEmpty(commandName)) return;
        if (SkippedCommands.Contains(commandName)) return;

        // Skip lines starting with the binary name — these are example/continuation lines
        // (e.g. "glab repo clone -g <group>" that follows the actual clone entry)
        if (commandName is "glab") return;

        var desc = parts[1].Trim();
        if (string.IsNullOrEmpty(desc)) return;

        builder.SubCommands.Add(new CommandNode { Name = commandName, Description = desc });
    }

    private static void ParseFlagLine(string line, CommandNodeBuilder builder)
    {
        // Formats across versions:
        //   Older: "  -v, --version   show glab version information"
        //   Newer: "  -v --version    show glab version information"
        //   Both:  "      --help      Show help for this command."

        string? shortName = null;
        string? longName = null;

        var current = line.AsSpan().Trim();

        // Parse optional short flag: -X or -X, (with optional comma)
        if (current.StartsWith("-") && !current.StartsWith("--"))
        {
            // Find end of short flag token (letter(s) + optional comma)
            var spaceIdx = current.IndexOf(' ');
            if (spaceIdx > 0)
            {
                shortName = current[1..spaceIdx].Trim().ToString();
                current = current[spaceIdx..].Trim();
            }
            else
            {
                shortName = current[1..].ToString();
                current = ReadOnlySpan<char>.Empty;
            }

            // Strip trailing comma from older format: "v," → "v"
            shortName = shortName.TrimEnd(',');

            // Skip comma-only separator that might remain after trimming
            if (current.StartsWith(","))
                current = current[1..].Trim();
        }

        // Parse --long-name
        if (current.StartsWith("--"))
        {
            var end = current.Length;
            for (var i = 2; i < current.Length; i++)
            {
                if (!IsValidOptionNameChar(current[i]))
                {
                    end = i;
                    break;
                }
            }
            longName = current[2..end].ToString();
            current = end < current.Length ? current[end..].Trim() : ReadOnlySpan<char>.Empty;
        }

        if (longName is null && shortName is null)
            return;

        // Everything remaining is the description
        var description = current.Length > 0 ? current.ToString().Trim() : null;

        // Determine value kind from description heuristics
        var valueKind = OptionValueKind.Flag;
        var clrType = "bool";
        string? defaultValue = null;

        if (description is not null)
        {
            // Check for default value in parentheses at end: "(created_at)", "(30)"
            var defaultMatch = DefaultValuePattern.Match(description);
            if (defaultMatch.Success)
            {
                defaultValue = defaultMatch.Groups[1].Value;
                description = description[..defaultMatch.Index].TrimEnd();
                valueKind = OptionValueKind.Single;
                clrType = int.TryParse(defaultValue, out _) ? "integer" : "string";
            }

            // Check for multiple-value indicators
            if (MultipleValuePattern.IsMatch(description))
            {
                valueKind = OptionValueKind.Multiple;
                clrType = "string";
            }
            // Check for placeholder indicating a value parameter
            else if (valueKind == OptionValueKind.Flag && PlaceholderPattern.IsMatch(description))
            {
                valueKind = OptionValueKind.Single;
                clrType = "string";
            }
        }

        builder.Options.Add(new OptionDefinition
        {
            LongName = longName ?? shortName!,
            ShortName = shortName,
            Description = description,
            ValueKind = valueKind,
            ClrType = clrType,
            DefaultValue = defaultValue
        });
    }

    private static bool IsValidOptionNameChar(char c) =>
        char.IsLetterOrDigit(c) || c == '-' || c == '_';
}
