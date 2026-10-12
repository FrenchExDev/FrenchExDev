using FrenchExDev.Net.BinaryWrapper.Design;

namespace FrenchExDev.Net.Git.Design;

/// <summary>
/// Parses Git help text. Handles three distinct formats:
/// <list type="bullet">
///   <item><b>Root discovery</b> (<c>git help -a</c>): Title Case section headers with indented command lines</item>
///   <item><b>Leaf commands</b> (<c>git commit -h</c>): usage line(s) + GNU-style options</item>
///   <item><b>Subcommand groups</b> (<c>git remote -h</c>): multiple usage/or lines with embedded subcommand names</item>
/// </list>
/// </summary>
public sealed class GitHelpParser : IHelpParser
{
    private enum Mode { Unknown, RootDiscovery, LeafOrGroup }

    private static readonly HashSet<string> SkippedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "help", "credential", "credential-cache", "credential-store"
    };

    public CommandNode? Parse(string helpText, string commandName)
    {
        if (string.IsNullOrWhiteSpace(helpText))
            return null;

        var lines = helpText.Split('\n');
        var mode = DetectMode(lines);

        return mode == Mode.RootDiscovery
            ? ParseRootDiscovery(lines, commandName)
            : ParseLeafOrGroup(lines, commandName);
    }

    private static Mode DetectMode(string[] lines)
    {
        // A "usage:" line anywhere means this is a leaf/group help text.
        // Only if NO usage line is found do we check for root discovery headers.
        // This avoids misclassifying commands whose help starts with warnings
        // (e.g., git filter-branch outputs WARNING: before usage:).
        var hasUsage = false;
        var hasRootHeader = false;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.TrimEnd('\r').Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            if (trimmed.StartsWith("usage:", StringComparison.OrdinalIgnoreCase))
                hasUsage = true;

            if (trimmed.StartsWith("See '", StringComparison.Ordinal))
                hasRootHeader = true;

            if (IsCommandSectionHeader(trimmed))
                hasRootHeader = true;
        }

        // usage: takes priority — it's definitely a leaf/group
        if (hasUsage)
            return Mode.LeafOrGroup;

        return hasRootHeader ? Mode.RootDiscovery : Mode.LeafOrGroup;
    }

    // ── Root discovery parsing (git help -a) ─────────────────────────────────

    private static CommandNode? ParseRootDiscovery(string[] lines, string commandName)
    {
        var builder = new CommandNodeBuilder(commandName);
        var inCommandSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                // Blank line ends current section
                inCommandSection = false;
                continue;
            }

            // Skip preamble lines
            if (trimmed.StartsWith("See '", StringComparison.Ordinal))
                continue;

            // Non-indented line = potential section header
            if (!line.StartsWith(' ') && !line.StartsWith('\t'))
            {
                inCommandSection = IsCommandSectionHeader(trimmed);
                continue;
            }

            // Indented line under a command section header = command
            if (inCommandSection)
            {
                ParseRootCommandLine(trimmed, builder);
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// Returns true only for section headers that contain actual commands.
    /// Returns false for documentation/guide sections like
    /// "User-facing repository, command and file interfaces" or
    /// "Developer-facing file formats, protocols and other interfaces".
    /// </summary>
    private static bool IsCommandSectionHeader(string trimmed)
    {
        if (trimmed.Length == 0) return false;
        if (trimmed[0] == '-' || trimmed[0] == ' ') return false;
        if (trimmed.StartsWith("usage:", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.StartsWith("See '", StringComparison.Ordinal)) return false;
        if (trimmed.StartsWith("Command aliases", StringComparison.OrdinalIgnoreCase)) return false;

        // Must start with an uppercase letter
        if (!char.IsUpper(trimmed[0])) return false;

        // Exclude documentation/guide sections (not commands)
        if (trimmed.Contains("interfaces", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.Contains("formats", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.Contains("concepts", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.Contains("guides", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.Contains("tutorials", StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    private static void ParseRootCommandLine(string trimmed, CommandNodeBuilder builder)
    {
        var parts = trimmed.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var name = parts[0];
        if (SkippedCommands.Contains(name)) return;

        var desc = parts.Length > 1 ? parts[1].Trim() : null;
        builder.SubCommands.Add(new CommandNode { Name = name, Description = desc });
    }

    // ── Leaf / subcommand group parsing ──────────────────────────────────────

    private static CommandNode? ParseLeafOrGroup(string[] lines, string commandName)
    {
        var builder = new CommandNodeBuilder(commandName);
        var usageLines = new List<string>();
        var inUsage = false;
        var inOptions = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (inUsage) inUsage = false;
                continue;
            }

            // Detect "usage:" line
            if (trimmed.StartsWith("usage:", StringComparison.OrdinalIgnoreCase))
            {
                inUsage = true;
                inOptions = false;
                usageLines.Add(trimmed);
                continue;
            }

            // "or:" continuation line (git uses "   or: git remote add ...")
            if (inUsage && (trimmed.StartsWith("or:", StringComparison.OrdinalIgnoreCase)
                         || (line.StartsWith(' ') && !trimmed.StartsWith('-'))))
            {
                usageLines.Add(trimmed);
                continue;
            }

            if (inUsage)
                inUsage = false;

            // Option line: starts with -
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && trimmed.StartsWith('-'))
            {
                inOptions = true;
                StandardHelpParser.ParseOptionLine(NormalizeOptionLine(trimmed), builder);
                continue;
            }

            // Continuation line for an option (deeper indent, no dash)
            if (inOptions && (line.StartsWith(' ') || line.StartsWith('\t')) && !trimmed.StartsWith('-'))
            {
                // Skip continuation lines (description wrap)
                continue;
            }

            // Non-option, non-usage indented line = section header like "Commit message options"
            inOptions = false;
        }

        // Deduplicate options that would produce the same PascalCase property name
        // (e.g., git annotate has both -c and -C which both map to "C")
        DeduplicateOptions(builder);

        // Extract positional arguments from the first usage line
        if (usageLines.Count > 0)
            ExtractArgumentsFromUsage(usageLines[0], commandName, builder);

        // Detect subcommand group: multiple usage lines with distinct subcommand names
        var subCommands = ExtractSubCommandsFromUsage(usageLines, commandName);
        if (subCommands.Count > 0)
        {
            foreach (var sub in subCommands)
            {
                if (!SkippedCommands.Contains(sub))
                    builder.SubCommands.Add(new CommandNode { Name = sub });
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// Extracts subcommand names from usage/or lines.
    /// For <c>git remote add [&lt;options&gt;] &lt;name&gt; &lt;url&gt;</c>,
    /// the subcommand is "add" (the token after the parent command name).
    /// </summary>
    private static List<string> ExtractSubCommandsFromUsage(List<string> usageLines, string commandName)
    {
        var subCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var usageLine in usageLines)
        {
            // Strip "usage:" or "or:" prefix
            var line = usageLine;
            if (line.StartsWith("usage:", StringComparison.OrdinalIgnoreCase))
                line = line["usage:".Length..].Trim();
            else if (line.StartsWith("or:", StringComparison.OrdinalIgnoreCase))
                line = line["or:".Length..].Trim();
            else
                line = line.Trim();

            // Tokenize: "git remote add [<options>] <name> <url>"
            var tokens = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

            // Find the position of the command name in the token list
            // e.g., for commandName="remote", find "remote" and take the next token
            var cmdIdx = -1;
            for (var i = 0; i < tokens.Length; i++)
            {
                if (string.Equals(tokens[i], commandName, StringComparison.OrdinalIgnoreCase))
                {
                    cmdIdx = i;
                    break;
                }
            }

            if (cmdIdx >= 0 && cmdIdx + 1 < tokens.Length)
            {
                var candidate = tokens[cmdIdx + 1];
                // Skip placeholders and alternation groups:
                // [<options>], <name>, [-v], (bad|new|<term>), --flag
                if (!candidate.StartsWith('[') && !candidate.StartsWith('<')
                    && !candidate.StartsWith('-') && !candidate.StartsWith('('))
                {
                    subCommands.Add(candidate);
                }
            }
        }

        // Only treat as subcommand group if there are multiple distinct subcommands
        return subCommands.Count >= 2 ? subCommands.ToList() : [];
    }

    /// <summary>
    /// Extracts positional arguments from a usage line.
    /// <c>usage: git add [&lt;options&gt;] [--] &lt;pathspec&gt;...</c>
    /// yields argument "pathspec" (optional, variadic) — arguments after [--] are always optional.
    /// </summary>
    private static void ExtractArgumentsFromUsage(string usageLine, string commandName, CommandNodeBuilder builder)
    {
        // Strip "usage:" prefix
        var line = usageLine;
        if (line.StartsWith("usage:", StringComparison.OrdinalIgnoreCase))
            line = line["usage:".Length..];

        var tokens = TokenizeUsageLine(line.Trim());
        var position = 0;

        // Skip tokens until we pass the command path (e.g., "git add")
        var pastCommand = false;
        var skipCount = 0;
        foreach (var token in tokens)
        {
            skipCount++;
            if (string.Equals(token, commandName, StringComparison.OrdinalIgnoreCase))
            {
                pastCommand = true;
                break;
            }
        }

        if (!pastCommand) return;

        // Arguments after [--] or -- are always optional (it's a pass-through separator)
        var afterDashDash = false;

        foreach (var token in tokens.Skip(skipCount))
        {
            // Track [--] separator — everything after it is optional
            if (token == "--" || token == "[--]") { afterDashDash = true; continue; }
            if (token.Contains("<options>", StringComparison.OrdinalIgnoreCase)) continue;
            // Skip option-like tokens: [-v], [--flag], etc.
            if (token.StartsWith("[-") || token.StartsWith("--")) continue;

            // Detect argument patterns: <name>, [<name>], <name>..., [<name>...]
            var isOptional = token.StartsWith('[') || afterDashDash;
            var clean = token.Trim('[', ']');
            var isVariadic = clean.EndsWith("...");
            clean = clean.TrimEnd('.');

            // Must be a <placeholder> to be an argument
            if (!clean.StartsWith('<') || !clean.EndsWith('>')) continue;

            var name = clean.Trim('<', '>');
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Skip names containing invalid chars (e.g., "old-base>..<old-tip" from range expressions)
            if (name.IndexOfAny(['.', '>', '<', '|', ' ']) >= 0) continue;

            // Skip duplicate argument names (SG handles option/reserved-name clashes)
            if (builder.Arguments.Any(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            builder.Arguments.Add(new ArgumentDefinition
            {
                Name = name,
                Position = position++,
                IsRequired = !isOptional,
                IsVariadic = isVariadic
            });
        }
    }

    /// <summary>
    /// Tokenizes a usage line respecting bracket groups.
    /// <c>git push [&lt;repository&gt; [&lt;refspec&gt;...]]</c> yields
    /// ["git", "push", "[&lt;repository&gt;", "[&lt;refspec&gt;...]]"]
    /// Nested brackets are flattened — each &lt;placeholder&gt; becomes its own token.
    /// </summary>
    private static List<string> TokenizeUsageLine(string line)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < line.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(line[i])) { i++; continue; }

            // Collect a token: everything up to next unbracketed whitespace
            var start = i;
            var bracketDepth = 0;
            while (i < line.Length && !(char.IsWhiteSpace(line[i]) && bracketDepth == 0))
            {
                if (line[i] == '[') bracketDepth++;
                else if (line[i] == ']') bracketDepth = Math.Max(0, bracketDepth - 1);
                i++;
            }

            var token = line[start..i];

            // Flatten nested bracket groups into individual argument tokens
            // e.g. "[<repository> [<refspec>...]]" → "[<repository>", "[<refspec>...]]"
            if (token.Contains(' '))
            {
                // Re-tokenize without bracket tracking
                foreach (var sub in token.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    tokens.Add(sub);
            }
            else
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    /// <summary>
    /// Normalizes git option lines for <see cref="StandardHelpParser.ParseOptionLine"/>.
    /// Git uses <c>-O&lt;file&gt;</c> and <c>-C[&lt;score&gt;]</c> (no space between
    /// short flag and value placeholder). Inserts a space so the parser treats the
    /// placeholder as a value, not part of the option name.
    /// </summary>
    private static string NormalizeOptionLine(string line)
    {
        // Pattern: -X<value> or -X[<value>] where X is a single letter
        // e.g. "-O<file>" → "-O <file>", "-C[<score>]" → "-C [<score>]"
        if (line.Length >= 4 && line[0] == '-' && line[1] != '-'
            && char.IsLetter(line[1]) && (line[2] == '<' || line[2] == '['))
        {
            return line[..2] + " " + line[2..];
        }

        return line;
    }

    /// <summary>
    /// Removes duplicate options that would produce the same PascalCase property name.
    /// Git has both <c>-c</c> and <c>-C</c> as separate options — both map to <c>C</c>
    /// in PascalCase, causing a compilation error. Keeps the first occurrence.
    /// </summary>
    private static void DeduplicateOptions(CommandNodeBuilder builder)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        builder.Options = builder.Options
            .Where(o => seen.Add(o.LongName))
            .ToList();
    }
}
