using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.Git.Design;
using Shouldly;

public class GitHelpParserRootDiscoveryTests
{
    private readonly GitHelpParser _parser = new();

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        _parser.Parse(null!, "git").ShouldBeNull();
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsNull()
    {
        _parser.Parse("", "git").ShouldBeNull();
    }

    [Fact]
    public void Parse_WhitespaceInput_ReturnsNull()
    {
        _parser.Parse("   \n  \n  ", "git").ShouldBeNull();
    }

    [Fact]
    public void Parse_RootDiscovery_ParsesCommandsUnderSectionHeaders()
    {
        var help = """
            See 'git help <command>' to read about a specific subcommand

            Main Porcelain Commands
               add                  Add file contents to the index
               commit               Record changes to the repository
               push                 Update remote refs along with objects

            Ancillary Commands
               config               Get and set repository or global options
               reflog               Manage reflog information
            """;

        var result = _parser.Parse(help, "git");

        result.ShouldNotBeNull();
        result.Name.ShouldBe("git");
        result.SubCommands.Count.ShouldBe(5);
        result.SubCommands[0].Name.ShouldBe("add");
        result.SubCommands[0].Description.ShouldBe("Add file contents to the index");
        result.SubCommands[1].Name.ShouldBe("commit");
        result.SubCommands[2].Name.ShouldBe("push");
        result.SubCommands[3].Name.ShouldBe("config");
        result.SubCommands[4].Name.ShouldBe("reflog");
    }

    [Fact]
    public void Parse_RootDiscovery_SkipsHelpCommand()
    {
        var help = """
            See 'git help <command>' to read about a specific subcommand

            Main Porcelain Commands
               add                  Add file contents to the index
               help                 Display help information
               commit               Record changes to the repository
            """;

        var result = _parser.Parse(help, "git");

        result.ShouldNotBeNull();
        result.SubCommands.Count.ShouldBe(2);
        result.SubCommands.ShouldNotContain(c => c.Name == "help");
    }

    [Fact]
    public void Parse_RootDiscovery_SkipsCredentialCommands()
    {
        var help = """
            See 'git help <command>' to read about a specific subcommand

            Main Porcelain Commands
               add                  Add file contents to the index

            External Commands
               credential           Retrieve and store user credentials
               credential-cache     Cache credentials in memory
               credential-store     Store credentials on disk
            """;

        var result = _parser.Parse(help, "git");

        result.ShouldNotBeNull();
        result.SubCommands.Count.ShouldBe(1);
        result.SubCommands[0].Name.ShouldBe("add");
    }

    [Fact]
    public void Parse_RootDiscovery_BlankLineSeparatesSections()
    {
        var help = """
            See 'git help <command>' to read about a specific subcommand

            Main Porcelain Commands
               clone                Clone a repository

            Low-level Commands / Internal Helpers
               cat-file             Provide content info for repository objects
            """;

        var result = _parser.Parse(help, "git");

        result.ShouldNotBeNull();
        result.SubCommands.Count.ShouldBe(2);
        result.SubCommands[0].Name.ShouldBe("clone");
        result.SubCommands[1].Name.ShouldBe("cat-file");
    }

    [Fact]
    public void Parse_RootDiscovery_IgnoresCommandAliasesSection()
    {
        var help = """
            See 'git help <command>' to read about a specific subcommand

            Main Porcelain Commands
               add                  Add file contents to the index

            Command aliases
               co                   checkout
            """;

        var result = _parser.Parse(help, "git");

        result.ShouldNotBeNull();
        // "Command aliases" header should NOT be treated as a command section
        // since IsRootSectionHeader returns false for it
        result.SubCommands.Count.ShouldBe(1);
        result.SubCommands[0].Name.ShouldBe("add");
    }
}

public class GitHelpParserLeafCommandTests
{
    private readonly GitHelpParser _parser = new();

    [Fact]
    public void Parse_LeafCommand_ParsesOptions()
    {
        var help = """
            usage: git add [<options>] [--] <pathspec>...

                -n, --dry-run         dry run
                -v, --verbose         be verbose
                --all                 add changes from all tracked files
            """;

        var result = _parser.Parse(help, "add");

        result.ShouldNotBeNull();
        result.Name.ShouldBe("add");
        result.Options.Count.ShouldBe(3);

        result.Options[0].LongName.ShouldBe("dry-run");
        result.Options[0].ShortName.ShouldBe("n");

        result.Options[1].LongName.ShouldBe("verbose");
        result.Options[1].ShortName.ShouldBe("v");

        result.Options[2].LongName.ShouldBe("all");
        result.Options[2].ShortName.ShouldBeNull();
    }

    [Fact]
    public void Parse_LeafCommand_ParsesOptionWithValue()
    {
        var help = """
            usage: git commit [<options>]

                -m, --message=<msg>   commit message
                --author=<author>     override author for commit
            """;

        var result = _parser.Parse(help, "commit");

        result.ShouldNotBeNull();
        result.Options.Count.ShouldBe(2);
        result.Options[0].LongName.ShouldBe("message");
        result.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        result.Options[1].LongName.ShouldBe("author");
    }

    [Fact]
    public void Parse_LeafCommand_SkipsOptionContinuationLines()
    {
        var help = """
            usage: git log [<options>]

                --format=<format>     pretty-print in a given format
                                      where <format> can be one of many things
                -n, --max-count=<n>   limit the number of commits
            """;

        var result = _parser.Parse(help, "log");

        result.ShouldNotBeNull();
        result.Options.Count.ShouldBe(2);
        result.Options[0].LongName.ShouldBe("format");
        result.Options[1].LongName.ShouldBe("max-count");
    }

    [Fact]
    public void Parse_LeafCommand_NoOptionsReturnsEmptyList()
    {
        var help = """
            usage: git status [<options>] [--] [<pathspec>...]
            """;

        var result = _parser.Parse(help, "status");

        result.ShouldNotBeNull();
        result.Options.Count.ShouldBe(0);
        result.SubCommands.Count.ShouldBe(0);
    }
}

public class GitHelpParserSubcommandGroupTests
{
    private readonly GitHelpParser _parser = new();

    [Fact]
    public void Parse_SubcommandGroup_ExtractsFromUsageLines()
    {
        var help = """
            usage: git remote [-v | --verbose]
               or: git remote add [-t <branch>] [-m <master>] <name> <url>
               or: git remote rename [--[no-]progress] <old> <new>
               or: git remote remove <name>
               or: git remote show [-n] <name>
               or: git remote prune [-n | --dry-run] <name>
               or: git remote set-url [--push] <name> <newurl>
               or: git remote get-url [--push] [--all] <name>
            """;

        var result = _parser.Parse(help, "remote");

        result.ShouldNotBeNull();
        result.SubCommands.Count.ShouldBeGreaterThan(1);
        result.SubCommands.ShouldContain(c => c.Name == "add");
        result.SubCommands.ShouldContain(c => c.Name == "rename");
        result.SubCommands.ShouldContain(c => c.Name == "remove");
        result.SubCommands.ShouldContain(c => c.Name == "show");
        result.SubCommands.ShouldContain(c => c.Name == "prune");
        result.SubCommands.ShouldContain(c => c.Name == "set-url");
        result.SubCommands.ShouldContain(c => c.Name == "get-url");
    }

    [Fact]
    public void Parse_SubcommandGroup_SingleUsageLine_TreatedAsLeaf()
    {
        // Only one usage line = not a subcommand group, just a leaf command
        var help = """
            usage: git branch [<options>] [<branch>]

                -d, --delete          delete fully merged branch
                -m, --move            move/rename a branch
            """;

        var result = _parser.Parse(help, "branch");

        result.ShouldNotBeNull();
        result.SubCommands.Count.ShouldBe(0);
        result.Options.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_SubcommandGroup_WithTrailingOptions()
    {
        var help = """
            usage: git stash list [<log-options>]
               or: git stash show [-u | --include-untracked] [<stash>]
               or: git stash drop [-q | --quiet] [<stash>]
               or: git stash pop [--index] [-q | --quiet] [<stash>]
               or: git stash apply [--index] [-q | --quiet] [<stash>]
               or: git stash branch <branchname> [<stash>]
               or: git stash push [-p | --patch] [-S] [-k] [<pathspec>...]
               or: git stash clear
               or: git stash create [<message>]
               or: git stash store [-m | --message <message>] [-q | --quiet] <commit>

                -q, --quiet           quiet mode
            """;

        var result = _parser.Parse(help, "stash");

        result.ShouldNotBeNull();
        result.SubCommands.Count.ShouldBeGreaterThan(1);
        result.SubCommands.ShouldContain(c => c.Name == "list");
        result.SubCommands.ShouldContain(c => c.Name == "push");
        result.SubCommands.ShouldContain(c => c.Name == "clear");
        // Should also parse trailing options
        result.Options.ShouldContain(o => o.LongName == "quiet");
    }
}
