using FrenchExDev.Net.BinaryWrapper.Design;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Tests;

public class GhStyleHelpParserTests
{
    private readonly GhStyleHelpParser _parser = new();

    // ── Null / Empty ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_Null_ReturnsNull()
    {
        _parser.Parse(null!, "cmd").ShouldBeNull();
    }

    [Fact]
    public void Parse_Empty_ReturnsNull()
    {
        _parser.Parse("", "cmd").ShouldBeNull();
    }

    [Fact]
    public void Parse_Whitespace_ReturnsNull()
    {
        _parser.Parse("   \n  \n  ", "cmd").ShouldBeNull();
    }

    // ── Description extraction ──────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsDescription_FirstNonHeaderLine()
    {
        var help = """
            GLab is an open source GitLab CLI tool.

            USAGE
              glab <command> [flags]

            COMMANDS
              repo  Work with repositories.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.Description.ShouldBe("GLab is an open source GitLab CLI tool.");
    }

    // ── COMMANDS section ────────────────────────────────────────────────────

    [Fact]
    public void Parse_CommandsSection_MultipleCommands()
    {
        var help = """
            A CLI tool.

            COMMANDS
              alias [command] [--flags]   Create, list, and delete aliases.
              api <endpoint> [--flags]    Make an authenticated API request.
              auth <command> [command]     Manage authentication state.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(3);
        node.SubCommands[0].Name.ShouldBe("alias");
        node.SubCommands[0].Description.ShouldBe("Create, list, and delete aliases.");
        node.SubCommands[1].Name.ShouldBe("api");
        node.SubCommands[1].Description.ShouldBe("Make an authenticated API request.");
        node.SubCommands[2].Name.ShouldBe("auth");
        node.SubCommands[2].Description.ShouldBe("Manage authentication state.");
    }

    // ── CORE COMMANDS header variant ────────────────────────────────────────

    [Fact]
    public void Parse_CoreCommandsHeader_ParsesAsCommands()
    {
        var help = """
            A CLI tool.

            CORE COMMANDS
              alias:   Create, list, and delete aliases.
              ci:      Work with CI/CD pipelines.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(2);
        node.SubCommands[0].Name.ShouldBe("alias");
        node.SubCommands[1].Name.ShouldBe("ci");
    }

    [Fact]
    public void Parse_OtherCommandsHeader_ParsesAsCommands()
    {
        var help = """
            A CLI tool.

            OTHER COMMANDS
              version   Show version information.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("version");
    }

    [Fact]
    public void Parse_AdditionalCommandsHeader_ParsesAsCommands()
    {
        var help = """
            A CLI tool.

            ADDITIONAL COMMANDS
              config   Manage settings.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("config");
    }

    // ── Colon format (older glab) ───────────────────────────────────────────

    [Fact]
    public void Parse_ColonFormat_StripsColonFromCommandName()
    {
        var help = """
            A CLI tool.

            CORE COMMANDS
              alias:       Create, list, and delete aliases.
              api:         Make an authenticated request.
              auth:        Manage authentication state.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(3);
        node.SubCommands[0].Name.ShouldBe("alias");
        node.SubCommands[0].Description.ShouldBe("Create, list, and delete aliases.");
        node.SubCommands[1].Name.ShouldBe("api");
        node.SubCommands[2].Name.ShouldBe("auth");
    }

    // ── Args format (newer glab/gh) ────────────────────────────────────────

    [Fact]
    public void Parse_ArgsFormat_ExtractsCommandNameOnly()
    {
        var help = """
            A CLI tool.

            COMMANDS
              alias [command] [--flags]    Create, list, and delete aliases.
              ci <command> [command]        Work with CI/CD.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(2);
        node.SubCommands[0].Name.ShouldBe("alias");
        node.SubCommands[1].Name.ShouldBe("ci");
    }

    // ── FLAGS section ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_Flags_SpaceSeparatedShortLong()
    {
        var help = """
            A CLI tool.

            FLAGS
              -v --version   Show version information.
              -h --help      Show help for this command.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(2);
        node.Options[0].ShortName.ShouldBe("v");
        node.Options[0].LongName.ShouldBe("version");
        node.Options[0].Description.ShouldBe("Show version information.");
        node.Options[1].ShortName.ShouldBe("h");
        node.Options[1].LongName.ShouldBe("help");
    }

    [Fact]
    public void Parse_Flags_CommaSeparatedShortLong()
    {
        var help = """
            A CLI tool.

            FLAGS
              -v, --version   show glab version information
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ShortName.ShouldBe("v");
        node.Options[0].LongName.ShouldBe("version");
    }

    [Fact]
    public void Parse_Flags_LongOnly()
    {
        var help = """
            A CLI tool.

            FLAGS
              --help   Show help for this command.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ShortName.ShouldBeNull();
        node.Options[0].LongName.ShouldBe("help");
        node.Options[0].Description.ShouldBe("Show help for this command.");
    }

    // ── INHERITED FLAGS section ─────────────────────────────────────────────

    [Fact]
    public void Parse_InheritedFlags_ParsedAsFlags()
    {
        var help = """
            A CLI tool.

            FLAGS
              -h --help   Show help for this command.

            INHERITED FLAGS
              --hostname   Hostname of the GitLab instance.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(2);
        node.Options[0].LongName.ShouldBe("help");
        node.Options[1].LongName.ShouldBe("hostname");
    }

    // ── Skipped commands ────────────────────────────────────────────────────

    [Fact]
    public void Parse_SkipsHelp_Completion_CheckUpdate()
    {
        var help = """
            A CLI tool.

            COMMANDS
              alias [command]     Create aliases.
              check-update        Check for latest releases.
              completion [flags]  Generate shell completion scripts.
              help [command]      Help about any command.
              repo [command]      Work with repositories.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(2);
        node.SubCommands[0].Name.ShouldBe("alias");
        node.SubCommands[1].Name.ShouldBe("repo");
    }

    [Fact]
    public void Parse_CustomSkippedCommands_OverridesDefaults()
    {
        var parser = new GhStyleHelpParser(skippedCommands: ["repo"]);

        var help = """
            A CLI tool.

            COMMANDS
              alias [command]     Create aliases.
              help [command]      Help about any command.
              repo [command]      Work with repositories.
            """;

        var node = parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        // "help" is NOT skipped because custom list overrides defaults
        node!.SubCommands.Count.ShouldBe(2);
        node.SubCommands[0].Name.ShouldBe("alias");
        node.SubCommands[1].Name.ShouldBe("help");
    }

    // ── Binary name filtering ───────────────────────────────────────────────

    [Fact]
    public void Parse_BinaryNameFiltering_SkipsContinuationLines()
    {
        var parser = new GhStyleHelpParser(binaryName: "glab");

        var help = """
            A CLI tool.

            COMMANDS
              alias [command]              Create aliases.
              glab repo clone -g <group>   Clone all repos in a group.
              repo [command]               Work with repositories.
            """;

        var node = parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(2);
        node.SubCommands[0].Name.ShouldBe("alias");
        node.SubCommands[1].Name.ShouldBe("repo");
    }

    [Fact]
    public void Parse_NoBinaryName_DoesNotFilter()
    {
        // Default parser has no binaryName
        var help = """
            A CLI tool.

            COMMANDS
              glab [command]   Meta command.
              repo [command]   Work with repositories.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(2);
    }

    // ── Value detection: default in parentheses ─────────────────────────────

    [Fact]
    public void Parse_Flag_DefaultInParentheses_SingleValueKind()
    {
        var help = """
            A CLI tool.

            FLAGS
              --order   Order by field. (created_at)
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("order");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].DefaultValue.ShouldBe("created_at");
        node.Options[0].ClrType.ShouldBe("string");
    }

    // ── Value detection: integer default ────────────────────────────────────

    [Fact]
    public void Parse_Flag_IntegerDefault_IntegerType()
    {
        var help = """
            A CLI tool.

            FLAGS
              -P --per-page   Number of items to list per page. (30)
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("per-page");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].DefaultValue.ShouldBe("30");
        node.Options[0].ClrType.ShouldBe("integer");
    }

    // ── Value detection: placeholder ────────────────────────────────────────

    [Fact]
    public void Parse_Flag_PlaceholderInDescription_SingleValueKind()
    {
        var help = """
            A CLI tool.

            FLAGS
              -a --assignee   Filter by assignee <username>.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("assignee");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].ClrType.ShouldBe("string");
    }

    // ── Value detection: comma-separated / repeating → Multiple ─────────────

    [Fact]
    public void Parse_Flag_CommaSeparated_MultipleValueKind()
    {
        var help = """
            A CLI tool.

            FLAGS
              -l --label   Filter by label. Multiple labels can be comma-separated or specified by repeating the flag.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("label");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
        node.Options[0].ClrType.ShouldBe("string");
    }

    [Fact]
    public void Parse_Flag_RepeatingTheFlag_MultipleValueKind()
    {
        var help = """
            A CLI tool.

            FLAGS
              --tag   Add a tag. Specify multiple tags by repeating the flag.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("tag");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
    }

    [Fact]
    public void Parse_Flag_MultipleKeyword_MultipleValueKind()
    {
        var help = """
            A CLI tool.

            FLAGS
              --env   Set multiple environment variables.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
    }

    // ── Value detection: no hints → Flag (bool) ────────────────────────────

    [Fact]
    public void Parse_Flag_NoHints_FlagValueKind()
    {
        var help = """
            A CLI tool.

            FLAGS
              -A --all   Get all issues.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("all");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
        node.Options[0].ClrType.ShouldBe("bool");
        node.Options[0].DefaultValue.ShouldBeNull();
    }

    // ── Skipped sections ────────────────────────────────────────────────────

    [Fact]
    public void Parse_EnvironmentVariablesSection_Skipped()
    {
        var help = """
            A CLI tool.

            COMMANDS
              repo [command]   Work with repositories.

            ENVIRONMENT VARIABLES
              GITLAB_TOKEN: An authentication token for API requests.
              GITLAB_HOST: Specify the URL of the GitLab server.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("repo");
    }

    [Fact]
    public void Parse_LearnMoreSection_Skipped()
    {
        var help = """
            A CLI tool.

            COMMANDS
              repo [command]   Work with repositories.

            LEARN MORE
              Use 'glab <command> --help' for more information.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_FeedbackSection_Skipped()
    {
        var help = """
            A CLI tool.

            COMMANDS
              repo [command]   Work with repositories.

            FEEDBACK
              Open an issue using 'glab issue create -R gitlab-org/cli'.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_ArgumentsSection_Skipped()
    {
        var help = """
            A CLI tool.

            ARGUMENTS
              <endpoint>   The API endpoint to call.

            FLAGS
              --verbose   Enable verbose output.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("verbose");
    }

    [Fact]
    public void Parse_JsonFieldsSection_Skipped()
    {
        var help = """
            A CLI tool.

            JSON FIELDS
              id, name, path

            FLAGS
              --output   Output format. (text)
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_HelpTopicsSection_Skipped()
    {
        var help = """
            A CLI tool.

            HELP TOPICS
              environment   About environment variables.

            FLAGS
              --verbose   Enable verbose output.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
    }

    // ── Usage / Examples / Aliases sections skipped ─────────────────────────

    [Fact]
    public void Parse_UsageSection_ContentSkipped()
    {
        var help = """
            A CLI tool.

            USAGE
              glab <command> [flags]

            COMMANDS
              repo [command]   Work with repositories.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("repo");
    }

    [Fact]
    public void Parse_ExamplesSection_ContentSkipped()
    {
        var help = """
            A CLI tool.

            EXAMPLES
              $ glab issue list --all
              $ glab issue ls --all

            COMMANDS
              issue [command]   Work with issues.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_AliasesSection_ContentSkipped()
    {
        var help = """
            A CLI tool.

            ALIASES
              ls

            FLAGS
              --all   Get all items.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
    }

    // ── Lines without description are skipped ───────────────────────────────

    [Fact]
    public void Parse_CommandLine_NoDoubleSpace_Skipped()
    {
        var help = """
            A CLI tool.

            COMMANDS
              repo [command]   Work with repositories.
              continuation-line-no-desc
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        // The continuation line has no double-space separation, so it should be skipped
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("repo");
    }

    // ── Default with parentheses and placeholder combined ───────────────────

    [Fact]
    public void Parse_Flag_DefaultOverridesPlaceholder()
    {
        // When a default is present AND a placeholder, default wins for value detection
        var help = """
            A CLI tool.

            FLAGS
              -O --output   Options: 'text' or 'json'. (text)
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].DefaultValue.ShouldBe("text");
        node.Options[0].ClrType.ShouldBe("string");
    }

    // ── Multiple value hint with default → Multiple wins ────────────────────

    [Fact]
    public void Parse_Flag_CommaSeparatedWithDefault_MultipleWins()
    {
        var help = """
            A CLI tool.

            FLAGS
              --not-label   Filter by lack of label. Multiple labels can be comma-separated.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
    }

    // ── Non-indented lines reset section ────────────────────────────────────

    [Fact]
    public void Parse_NonIndentedLine_ResetsSection()
    {
        var help = """
            A CLI tool.

            COMMANDS
              repo [command]   Work with repositories.
            This is not indented and resets the section.
              alias [command]   Create aliases.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        // "repo" is parsed, then the non-indented line resets section to None,
        // and "alias" line (indented but section is None) is not parsed as a command
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("repo");
    }

    // ── Real-world: glab 1.47.0 root (colon format) ────────────────────────

    [Fact]
    public void Parse_RealWorld_Glab147_ColonFormat()
    {
        var help = """
            GLab is an open source GitLab CLI tool that brings GitLab to your command line.

            USAGE
              glab <command> <subcommand> [flags]

            CORE COMMANDS
              alias:       Create, list, and delete aliases.
              api:         Make an authenticated request to the GitLab API.
              auth:        Manage glab's authentication state.
              changelog:   Interact with the changelog API.
              check-update: Check for latest glab releases.
              ci:          Work with GitLab CI/CD pipelines and jobs.
              completion:  Generate shell completion scripts.
              help:        Help about any command
              issue:       Work with GitLab issues.
              mr:          Create, view, and manage merge requests.
              repo:        Work with GitLab repositories and projects.
              version:     Show version information for glab.

            FLAGS
                  --help      Show help for this command.
              -v, --version   show glab version information
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.Description.ShouldBe("GLab is an open source GitLab CLI tool that brings GitLab to your command line.");

        // help, completion, check-update are skipped
        var names = node.SubCommands.Select(c => c.Name).ToList();
        names.ShouldNotContain("help");
        names.ShouldNotContain("completion");
        names.ShouldNotContain("check-update");
        names.ShouldContain("alias");
        names.ShouldContain("api");
        names.ShouldContain("auth");
        names.ShouldContain("ci");
        names.ShouldContain("issue");
        names.ShouldContain("mr");
        names.ShouldContain("repo");
        names.ShouldContain("version");

        // Flags
        node.Options.Count.ShouldBe(2);
        node.Options[0].LongName.ShouldBe("help");
        node.Options[0].ShortName.ShouldBeNull();
        node.Options[1].LongName.ShouldBe("version");
        node.Options[1].ShortName.ShouldBe("v");
    }

    // ── Real-world: glab 1.89.0 root (args format) ─────────────────────────

    [Fact]
    public void Parse_RealWorld_Glab189_ArgsFormat()
    {
        var help = """
            GLab is an open source GitLab CLI tool that brings GitLab to your command line.

            USAGE
              glab <command> <subcommand> [command] [--flags]

            COMMANDS
              alias [command] [--flags]                        Create, list, and delete aliases.
              api <endpoint> [--flags]                         Make an authenticated request to the GitLab API.
              auth <command> [command]                         Manage glab's authentication state.
              check-update                                     Check for latest glab releases.
              ci <command> [command] [--flags]                 Work with GitLab CI/CD pipelines and jobs.
              completion [--flags]                             Generate shell completion scripts.
              config [command] [--flags]                       Manage glab settings.
              help [command]                                   Help about any command
              issue [command] [--flags]                        Work with GitLab issues.
              mr <command> [command] [--flags]                 Create, view, and manage merge requests.
              release <command> [command] [--flags]            Manage GitLab releases.
              repo <command> [command] [--flags]               Work with GitLab repositories and projects.
              version                                          Show version information for glab.

            FLAGS
              -h --help                                        Show help for this command.
              -v --version                                     Show glab version information
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();

        var names = node.SubCommands.Select(c => c.Name).ToList();
        names.ShouldNotContain("help");
        names.ShouldNotContain("completion");
        names.ShouldNotContain("check-update");
        names.ShouldContain("alias");
        names.ShouldContain("api");
        names.ShouldContain("auth");
        names.ShouldContain("ci");
        names.ShouldContain("config");
        names.ShouldContain("issue");
        names.ShouldContain("mr");
        names.ShouldContain("release");
        names.ShouldContain("repo");
        names.ShouldContain("version");

        node.Options.Count.ShouldBe(2);
        node.Options[0].ShortName.ShouldBe("h");
        node.Options[0].LongName.ShouldBe("help");
        node.Options[1].ShortName.ShouldBe("v");
        node.Options[1].LongName.ShouldBe("version");
    }

    // ── Real-world: issue list flags ────────────────────────────────────────

    [Fact]
    public void Parse_RealWorld_IssueListFlags_ValueDetection()
    {
        var help = """
            List project issues.

            FLAGS
              -A --all            Get all issues.
              -a --assignee       Filter issue by assignee <username>.
              -l --label          Filter issue by label <name>. Multiple labels can be comma-separated or specified by repeating the flag.
              --order             Order issue by <field>. (created_at)
              -P --per-page       Number of items to list per page. (30)
              -O --output         Options: 'text' or 'json'. (text)
            """;

        var node = _parser.Parse(help, "list");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(6);

        // --all: no hints → Flag
        var all = node.Options.First(o => o.LongName == "all");
        all.ValueKind.ShouldBe(OptionValueKind.Flag);
        all.ClrType.ShouldBe("bool");

        // --assignee: <username> placeholder → Single
        var assignee = node.Options.First(o => o.LongName == "assignee");
        assignee.ValueKind.ShouldBe(OptionValueKind.Single);
        assignee.ClrType.ShouldBe("string");

        // --label: "comma-separated" + "repeating the flag" → Multiple
        var label = node.Options.First(o => o.LongName == "label");
        label.ValueKind.ShouldBe(OptionValueKind.Multiple);

        // --order: default (created_at) → Single, string
        var order = node.Options.First(o => o.LongName == "order");
        order.ValueKind.ShouldBe(OptionValueKind.Single);
        order.DefaultValue.ShouldBe("created_at");
        order.ClrType.ShouldBe("string");

        // --per-page: default (30) → Single, integer
        var perPage = node.Options.First(o => o.LongName == "per-page");
        perPage.ValueKind.ShouldBe(OptionValueKind.Single);
        perPage.DefaultValue.ShouldBe("30");
        perPage.ClrType.ShouldBe("integer");

        // --output: default (text) → Single, string
        var output = node.Options.First(o => o.LongName == "output");
        output.ValueKind.ShouldBe(OptionValueKind.Single);
        output.DefaultValue.ShouldBe("text");
        output.ClrType.ShouldBe("string");
    }

    // ── Command name is set correctly ───────────────────────────────────────

    [Fact]
    public void Parse_SetsCommandName()
    {
        var help = """
            A CLI tool.

            COMMANDS
              repo [command]   Work with repositories.
            """;

        var node = _parser.Parse(help, "my-command");
        node.ShouldNotBeNull();
        node!.Name.ShouldBe("my-command");
    }

    // ── Empty COMMANDS section ──────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyCommandsSection_NoSubCommands()
    {
        var help = """
            A CLI tool.

            COMMANDS

            FLAGS
              --verbose   Enable verbose output.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.SubCommands.ShouldBeEmpty();
        node.Options.Count.ShouldBe(1);
    }

    // ── Multiple sections combined ──────────────────────────────────────────

    [Fact]
    public void Parse_MultipleSections_AllParsed()
    {
        var help = """
            A CLI tool.

            USAGE
              glab <command> [flags]

            CORE COMMANDS
              alias:   Create aliases.
              repo:    Work with repositories.

            OTHER COMMANDS
              version:   Show version information.

            FLAGS
              -h --help      Show help for this command.
              -v --version   Show version information.

            INHERITED FLAGS
              --hostname   Hostname of the GitLab instance.

            ENVIRONMENT VARIABLES
              GITLAB_TOKEN: An authentication token.

            LEARN MORE
              Use 'glab <command> --help' for more information.

            FEEDBACK
              Open an issue.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(3); // alias, repo, version
        node.Options.Count.ShouldBe(3); // help, version, hostname
    }

    // ── Skipped commands are case-insensitive ───────────────────────────────

    [Fact]
    public void Parse_SkippedCommands_CaseInsensitive()
    {
        var help = """
            A CLI tool.

            COMMANDS
              Help [command]        Help about any command.
              COMPLETION [flags]    Generate completions.
              Check-Update          Check for updates.
              repo [command]        Work with repositories.
            """;

        var node = _parser.Parse(help, "glab");
        node.ShouldNotBeNull();
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("repo");
    }
}
