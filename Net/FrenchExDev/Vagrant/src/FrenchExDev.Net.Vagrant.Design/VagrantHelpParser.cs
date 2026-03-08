using FrenchExDev.Net.BinaryWrapper.Design;

namespace FrenchExDev.Net.Vagrant.Design;

/// <summary>
/// Parses Vagrant help text. Vagrant uses:
/// - "Common commands:" or "Available subcommands:" for commands
/// - "Options:" for options
/// - GNU-style --[no-]flag, --flag VALUE, -s, --short SHORT
/// </summary>
public sealed class VagrantHelpParser : IHelpParser
{
    private enum Section { None, Usage, Commands, Options }

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

            if (IsCommandsHeader(trimmed))
            { section = Section.Commands; continue; }
            if (IsOptionsHeader(trimmed))
            { section = Section.Options; continue; }
            if (trimmed.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase))
            { section = Section.Usage; continue; }

            // Non-indented line outside a section → description or section break
            if (section != Section.None && !line.StartsWith(' ') && !line.StartsWith('\t'))
            {
                section = Section.None;
                continue;
            }

            if (section == Section.None)
            {
                description ??= trimmed;
                continue;
            }

            switch (section)
            {
                case Section.Commands:
                    ParseCommandLine(trimmed, builder);
                    break;
                case Section.Options:
                    StandardHelpParser.ParseOptionLine(trimmed, builder);
                    break;
            }
        }

        builder.Description = description;
        return builder.Build();
    }

    private static bool IsCommandsHeader(string trimmed)
    {
        return trimmed.StartsWith("Common commands", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Available subcommands", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Subcommands", StringComparison.OrdinalIgnoreCase)
            || (trimmed.StartsWith("Commands", StringComparison.OrdinalIgnoreCase)
                && (trimmed.Length == 8 || trimmed[8] == ':'));
    }

    private static bool IsOptionsHeader(string trimmed)
    {
        return trimmed.StartsWith("Options:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Global options:", StringComparison.OrdinalIgnoreCase);
    }

    // Commands that echo root/parent help (infinite recursion) or hang (serve starts a GRPC server)
    private static readonly HashSet<string> SkippedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "help", "list-commands", "serve"
    };

    private static void ParseCommandLine(string line, CommandNodeBuilder builder)
    {
        var parts = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        var name = parts[0];
        if (SkippedCommands.Contains(name)) return;
        var desc = parts.Length > 1 ? parts[1].Trim() : null;
        builder.SubCommands.Add(new CommandNode { Name = name, Description = desc });
    }
}
