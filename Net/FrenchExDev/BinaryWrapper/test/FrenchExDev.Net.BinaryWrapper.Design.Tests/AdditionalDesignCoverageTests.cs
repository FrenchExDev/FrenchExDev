using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Testing;
using Shouldly;

namespace FrenchExDev.Net.BinaryWrapper.Design.Tests;

// ── GitHubTagsVersionCollector Tests ─────────────────────────────────────────

public class GitHubTagsVersionCollectorTests
{
    private static HttpClient CreateMockHttpClient(string jsonResponse, string? linkHeader = null)
    {
        var handler = new FakeHttpHandler(jsonResponse, linkHeader);
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "test");
        return client;
    }

    [Fact]
    public async Task CollectVersions_ParsesTagNames_StripsVPrefix()
    {
        var json = """[{"name": "v1.0.0"}, {"name": "v2.0.0"}, {"name": "v1.5.0"}]""";
        var collector = new GitHubTagsVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json));

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0", "1.5.0", "2.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_FiltersPreReleaseTags()
    {
        var json = """
        [
            {"name": "v1.0.0"},
            {"name": "v2.0.0-rc.1"},
            {"name": "v1.5.0-beta"}
        ]
        """;
        var collector = new GitHubTagsVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json));

        var versions = await collector.CollectVersionsAsync();

        // Default tagToVersion excludes tags with '-'
        versions.Count.ShouldBe(1);
        versions[0].ShouldBe("1.0.0");
    }

    [Fact]
    public async Task CollectVersions_EmptyArray_ReturnsEmpty()
    {
        var collector = new GitHubTagsVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient("[]"));

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBeEmpty();
    }

    [Fact]
    public async Task CollectVersions_CustomTagToVersion()
    {
        var json = """[{"name": "release-1.0.0"}, {"name": "release-2.0.0"}]""";
        var collector = new GitHubTagsVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json),
            tagToVersion: tag => tag.StartsWith("release-") ? tag["release-".Length..] : null);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0", "2.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_NullTagToVersion_SkipsEntry()
    {
        var json = """[{"name": "v1.0.0"}, {"name": "not-a-version"}]""";
        var collector = new GitHubTagsVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json),
            tagToVersion: tag => tag.StartsWith('v') ? tag[1..] : null);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_SkipsNullNameProperty()
    {
        var json = """[{"name": "v1.0.0"}, {"name": null}, {"other": "no name"}]""";
        var collector = new GitHubTagsVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json));

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_TagWithoutVPrefix_PassedThrough()
    {
        var json = """[{"name": "3.0.0"}]""";
        var collector = new GitHubTagsVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json));

        var versions = await collector.CollectVersionsAsync();

        // "3.0.0" contains no '-', so it passes through
        versions.ShouldBe(["3.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_Pagination_FollowsLinkHeader()
    {
        var page1Json = """[{"name": "v1.0.0"}]""";
        var page2Json = """[{"name": "v2.0.0"}]""";

        var handler = new SequencingHttpHandler([
            (page1Json, "<https://api.github.com/repos/o/r/tags?page=2>; rel=\"next\""),
            (page2Json, null)
        ]);
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "test");
        var collector = new GitHubTagsVersionCollector("o", "r", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0", "2.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_SortsSemantically()
    {
        var json = """
        [
            {"name": "v1.10.0"},
            {"name": "v1.2.0"},
            {"name": "v1.9.0"},
            {"name": "v2.0.0"}
        ]
        """;
        var collector = new GitHubTagsVersionCollector("owner", "repo",
            httpClient: CreateMockHttpClient(json));

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.2.0", "1.9.0", "1.10.0", "2.0.0"]);
    }
}

// ── GitHubReleasesVersionCollector Pagination Tests ───────────────────────────

public class GitHubReleasesVersionCollectorPaginationTests
{
    [Fact]
    public async Task CollectVersions_Pagination_FollowsLinkHeader()
    {
        var page1Json = """[{"tag_name": "v1.0.0", "prerelease": false}]""";
        var page2Json = """[{"tag_name": "v2.0.0", "prerelease": false}]""";

        var handler = new SequencingHttpHandler([
            (page1Json, "<https://api.github.com/repos/o/r/releases?page=2>; rel=\"next\""),
            (page2Json, null)
        ]);
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "test");
        var collector = new GitHubReleasesVersionCollector("o", "r", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0", "2.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_SkipsNullTagName()
    {
        var json = """
        [
            {"tag_name": "v1.0.0", "prerelease": false},
            {"tag_name": null, "prerelease": false},
            {"other": "no tag_name"}
        ]
        """;
        var handler = new FakeHttpHandler(json);
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "test");
        var collector = new GitHubReleasesVersionCollector("owner", "repo", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_EmptyTagToVersion_SkipsEntry()
    {
        var json = """[{"tag_name": "v1.0.0", "prerelease": false}, {"tag_name": "skip-me", "prerelease": false}]""";
        var handler = new FakeHttpHandler(json);
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "test");
        var collector = new GitHubReleasesVersionCollector("owner", "repo",
            httpClient: client,
            tagToVersion: tag => tag.StartsWith('v') ? tag[1..] : "");

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["1.0.0"]);
    }

    [Fact]
    public async Task CollectVersions_TagWithoutVPrefix_PassedThrough()
    {
        var json = """[{"tag_name": "3.0.0", "prerelease": false}]""";
        var handler = new FakeHttpHandler(json);
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Add("User-Agent", "test");
        var collector = new GitHubReleasesVersionCollector("owner", "repo", httpClient: client);

        var versions = await collector.CollectVersionsAsync();

        versions.ShouldBe(["3.0.0"]);
    }
}

// ── CompareVersionStrings Edge Cases ─────────────────────────────────────────

public class CompareVersionStringsEdgeCaseTests
{
    [Fact]
    public void CompareVersionStrings_EqualVersions_ReturnsZero()
    {
        GitHubReleasesVersionCollector.CompareVersionStrings("1.0.0", "1.0.0").ShouldBe(0);
    }

    [Fact]
    public void CompareVersionStrings_DifferentLengths_PadsWithZero()
    {
        // "1.0" vs "1.0.0" should be equal
        GitHubReleasesVersionCollector.CompareVersionStrings("1.0", "1.0.0").ShouldBe(0);
    }

    [Fact]
    public void CompareVersionStrings_WithPreReleaseSuffix_FallsBackToStringCompare()
    {
        // "1.0.0-alpha" vs "1.0.0-beta" - the '-' splits into 4 parts, "alpha" vs "beta" compared as strings
        var result = GitHubReleasesVersionCollector.CompareVersionStrings("1.0.0-alpha", "1.0.0-beta");
        result.ShouldBeLessThan(0);
    }

    [Fact]
    public void CompareVersionStrings_NonNumericParts_ComparedAsStrings()
    {
        var result = GitHubReleasesVersionCollector.CompareVersionStrings("1.0.0-rc1", "1.0.0-rc2");
        result.ShouldBeLessThan(0);
    }

    [Fact]
    public void CompareVersionStrings_DifferentMajor()
    {
        GitHubReleasesVersionCollector.CompareVersionStrings("2.0.0", "1.0.0").ShouldBeGreaterThan(0);
    }

    [Fact]
    public void CompareVersionStrings_DifferentMinor()
    {
        GitHubReleasesVersionCollector.CompareVersionStrings("1.5.0", "1.3.0").ShouldBeGreaterThan(0);
    }
}

// ── ScrapeAllCommand Tests ───────────────────────────────────────────────────

public class ScrapeAllCommandTests
{
    [Fact]
    public async Task ExecuteAsync_NoBinary_PrintsUsageAndReturnsError()
    {
        var cmd = new ScrapeAllCommand();
        cmd.Name.ShouldBe("scrape-all");
        cmd.Description.ShouldNotBeNullOrEmpty();
        var result = await cmd.ExecuteAsync([]);
        result.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithStaticVersions_NoCollector_ReturnsError()
    {
        // --binary specified but no --versions and no --collector
        var cmd = new ScrapeAllCommand();
        var result = await cmd.ExecuteAsync(["--binary", "test"]);
        result.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRepoFormat_ReturnsError()
    {
        var cmd = new ScrapeAllCommand();
        var result = await cmd.ExecuteAsync([
            "--binary", "test",
            "--collector", "github",
            "--repo", "invalid-no-slash"
        ]);
        result.ShouldBe(1);
    }
}

// ── ScrapePipeline Additional Coverage ───────────────────────────────────────

public class ScrapePipelineAdditionalTests
{
    [Fact]
    public async Task ExecuteAsync_UseParserByStrategy_Works()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .UseParser("standard")
            .WithRunHelp(_ => Task.FromResult("A test tool\n"));

        var tree = await pipeline.ExecuteAsync();

        tree.BinaryName.ShouldBe("tool");
        tree.Root.Description.ShouldBe("A test tool");
    }

    [Fact]
    public async Task ExecuteAsync_UseParserGeneric_Works()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .UseParser<PackerHelpParser>()
            .WithRunHelp(_ => Task.FromResult("A test tool\n"));

        var tree = await pipeline.ExecuteAsync();

        tree.BinaryName.ShouldBe("tool");
    }

    [Fact]
    public async Task ExecuteAsync_DefaultParser_WhenNoneSpecified()
    {
        // When no parser is specified, defaults to StandardHelpParser
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .WithRunHelp(_ => Task.FromResult("A test tool\n\nCommands:\n  sub    A sub\n"));

        var tree = await pipeline.ExecuteAsync();

        tree.Root.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithRuntimeString_SetsRuntime()
    {
        // WithRuntime(string) creates a ProcessRunnerContainerRuntime but we can test the Dockerfile path
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .WithRuntime("docker")
            .FromImage("alpine:3.19")
            .Install("apk add tool");

        var dockerfile = pipeline.GenerateDockerfile();
        dockerfile.ShouldContain("FROM alpine:3.19");
    }

    [Fact]
    public async Task ExecuteAsync_DumpHelpTo_CreatesFiles()
    {
        var dumpDir = Path.Combine(Path.GetTempPath(), $"bw-dump-{Guid.NewGuid()}");
        try
        {
            var pipeline = new ScrapePipeline()
                .Binary("tool")
                .DumpHelpTo(dumpDir)
                .WithRunHelp(_ => Task.FromResult("A test tool\n"));

            await pipeline.ExecuteAsync();

            Directory.Exists(dumpDir).ShouldBeTrue();
            // The help dump file should exist with format: tool.help.txt
            Directory.GetFiles(dumpDir, "*.help.txt").Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(dumpDir))
                Directory.Delete(dumpDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ScrapeParallelism_Configurable()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .ScrapeParallelism(1)
            .WithRunHelp(args =>
            {
                var key = string.Join(" ", args);
                return Task.FromResult(key switch
                {
                    "tool --help" => "Commands:\n  a    First\n  b    Second\n",
                    "tool a --help" => "First sub\n",
                    "tool b --help" => "Second sub\n",
                    _ => ""
                });
            });

        var tree = await pipeline.ExecuteAsync();
        tree.Root.SubCommands.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_OnCommandScraped_InvokesCallback()
    {
        var callbackCount = 0;
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .OnCommandScraped(() => Interlocked.Increment(ref callbackCount))
            .WithRunHelp(args =>
            {
                var key = string.Join(" ", args);
                return Task.FromResult(key switch
                {
                    "tool --help" => "Commands:\n  sub    A sub\n",
                    "tool sub --help" => "A sub\n",
                    _ => ""
                });
            });

        await pipeline.ExecuteAsync();

        callbackCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_TransformCommand_ByFullPath()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .WithRunHelp(args =>
            {
                var key = string.Join(" ", args);
                return Task.FromResult(key switch
                {
                    "tool --help" => "Commands:\n  group    A group\n",
                    "tool group --help" => "Commands:\n  leaf    A leaf\n",
                    "tool group leaf --help" => "A leaf\n\nOptions:\n  --flag    A flag\n",
                    _ => ""
                });
            })
            .TransformCommand("tool.group.leaf", node =>
            {
                var b = CommandNodeBuilder.From(node);
                b.SetOptionType("flag", "int");
                return b.Build();
            });

        var tree = await pipeline.ExecuteAsync();
        tree.Root.SubCommands[0].SubCommands[0].Options[0].ClrType.ShouldBe("int");
    }

    [Fact]
    public void ApplyTransforms_MultipleRootTransforms_AppliedInOrder()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .TransformRoot(root => new CommandNode
            {
                Name = root.Name,
                Description = "first",
                Options = root.Options,
                Arguments = root.Arguments,
                SubCommands = root.SubCommands
            })
            .TransformRoot(root => new CommandNode
            {
                Name = root.Name,
                Description = root.Description + "+second",
                Options = root.Options,
                Arguments = root.Arguments,
                SubCommands = root.SubCommands
            });

        var tree = new CommandTree
        {
            BinaryName = "tool",
            Root = new CommandNode { Name = "tool", Description = "original" }
        };

        var result = pipeline.ApplyTransforms(tree);
        result.Root.Description.ShouldBe("first+second");
    }

    [Fact]
    public void ApplyTransforms_CommandTransformByNameOnly()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .TransformCommand("build", node => new CommandNode
            {
                Name = node.Name,
                Description = "transformed",
                Options = node.Options,
                Arguments = node.Arguments,
                SubCommands = node.SubCommands
            });

        var tree = new CommandTree
        {
            BinaryName = "tool",
            Root = new CommandNodeBuilder("tool")
                .AddSubCommand("build", "original")
                .Build()
        };

        var result = pipeline.ApplyTransforms(tree);
        result.Root.SubCommands[0].Description.ShouldBe("transformed");
    }

    [Fact]
    public void ApplyTransforms_NoTransforms_ReturnsEquivalentTree()
    {
        var pipeline = new ScrapePipeline().Binary("tool");

        var tree = new CommandTree
        {
            BinaryName = "tool",
            Version = "1.0",
            Root = new CommandNode { Name = "tool", Description = "desc" }
        };

        var result = pipeline.ApplyTransforms(tree);
        result.BinaryName.ShouldBe("tool");
        result.Version.ShouldBe("1.0");
        result.Root.Description.ShouldBe("desc");
    }

    [Fact]
    public void GenerateDockerfile_MultipleInstallsAndBuildArgs()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .FromImage("ubuntu:24.04")
            .BuildArg("VERSION", "1.0")
            .BuildArg("ARCH", "amd64")
            .Install("apt-get update")
            .Install("apt-get install -y tool");

        var dockerfile = pipeline.GenerateDockerfile();

        dockerfile.ShouldContain("FROM ubuntu:24.04");
        dockerfile.ShouldContain("ARG VERSION=1.0");
        dockerfile.ShouldContain("ARG ARCH=amd64");
        dockerfile.ShouldContain("RUN apt-get update");
        dockerfile.ShouldContain("RUN apt-get install -y tool");
    }

    [Fact]
    public async Task ExecuteAsync_ContainerRuntime_CleansUpOnFailure()
    {
        var removeCalled = false;
        var mockRuntime = new MockContainerRuntime(
            onBuild: (_, _) => Task.FromResult("ok"),
            onRun: (_, _) => throw new Exception("container crashed"),
            onRemove: _ => { removeCalled = true; return Task.CompletedTask; });

        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .FromImage("alpine:3.19")
            .WithRuntime(mockRuntime);

        // The scraper catches exceptions internally and returns a fallback tree
        var tree = await pipeline.ExecuteAsync();

        removeCalled.ShouldBeTrue();
        tree.BinaryName.ShouldBe("tool");
    }
}

// ── HelpScraper Additional Coverage ──────────────────────────────────────────

public class HelpScraperAdditionalTests
{
    [Fact]
    public async Task ScrapeAsync_WithHelpDumpDir_DumpsHelpText()
    {
        var dumpDir = Path.Combine(Path.GetTempPath(), $"bw-helpdump-{Guid.NewGuid()}");
        try
        {
            var scraper = new HelpScraper(
                new StandardHelpParser(),
                _ => Task.FromResult("A simple tool\n"),
                helpDumpDir: dumpDir);

            await scraper.ScrapeAsync("tool");

            Directory.Exists(dumpDir).ShouldBeTrue();
            var files = Directory.GetFiles(dumpDir, "*.help.txt");
            files.Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            if (Directory.Exists(dumpDir))
                Directory.Delete(dumpDir, true);
        }
    }

    [Fact]
    public async Task ScrapeAsync_OnCommandScrapedCallback_Invoked()
    {
        var count = 0;
        var scraper = new HelpScraper(
            new StandardHelpParser(),
            _ => Task.FromResult("A simple tool\n"),
            onCommandScraped: () => count++);

        await scraper.ScrapeAsync("tool");

        count.ShouldBe(1);
    }

    [Fact]
    public async Task ScrapeAsync_MaxConcurrency_Respected()
    {
        var maxConcurrent = 0;
        var currentConcurrent = 0;

        var helpTexts = new Dictionary<string, string>
        {
            ["tool --help"] = "Commands:\n  a    First\n  b    Second\n  c    Third\n",
            ["tool a --help"] = "First\n",
            ["tool b --help"] = "Second\n",
            ["tool c --help"] = "Third\n"
        };

        var scraper = new HelpScraper(
            new StandardHelpParser(),
            async args =>
            {
                var c = Interlocked.Increment(ref currentConcurrent);
                var snapshot = c;
                while (snapshot > Volatile.Read(ref maxConcurrent))
                    Interlocked.CompareExchange(ref maxConcurrent, snapshot, Volatile.Read(ref maxConcurrent));
                await Task.Delay(20);
                Interlocked.Decrement(ref currentConcurrent);
                return helpTexts.GetValueOrDefault(string.Join(" ", args), "");
            },
            maxConcurrency: 1);

        await scraper.ScrapeAsync("tool");

        maxConcurrent.ShouldBeInRange(1, 1);
    }

    [Fact]
    public async Task ScrapeAsync_ParserReturnsNull_FallbackToStub()
    {
        var scraper = new HelpScraper(
            new StandardHelpParser(),
            args =>
            {
                var key = string.Join(" ", args);
                return Task.FromResult(key switch
                {
                    "tool --help" => "Commands:\n  sub    A sub\n",
                    "tool sub --help" => "   ", // whitespace only, parser returns null
                    _ => ""
                });
            });

        var tree = await scraper.ScrapeAsync("tool");

        tree.Root.SubCommands.Count.ShouldBe(1);
        tree.Root.SubCommands[0].Name.ShouldBe("sub");
        // When parser returns null, falls back to original stub
        tree.Root.SubCommands[0].Description.ShouldBe("A sub");
    }
}

// ── CobraHelpParser Additional Coverage ──────────────────────────────────────

public class CobraHelpParserAdditionalTests
{
    private readonly CobraHelpParser _parser = new();

    [Fact]
    public void Parse_AliasesSection_Skipped()
    {
        var help = """
            Run a container

            Aliases:
              run, start

            Flags:
              -d, --detach    Detached mode
            """;

        var node = _parser.Parse(help, "run");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("detach");
    }

    [Fact]
    public void Parse_AdditionalCommandsHeader()
    {
        var help = """
            Additional Commands:
              plugin    Manage plugins
            """;

        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("plugin");
    }

    [Fact]
    public void Parse_CommandsHeaderExact()
    {
        var help = """
            Commands:
              run    Run a container
            """;

        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_OptionsHeader()
    {
        var help = """
            Options:
              --verbose    Be verbose
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_GlobalOptionsHeader()
    {
        var help = """
            Global Options:
              --debug    Enable debug
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_ShortFlagOnly_NoLong()
    {
        var help = """
            Flags:
              -v    Version
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ShortName.ShouldBe("v");
        node.Options[0].LongName.ShouldBe("v"); // short used as long when no long present
    }

    [Fact]
    public void Parse_FloatType_MapToString()
    {
        var help = """
            Flags:
              --rate float64    Rate limit
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].ClrType.ShouldBe("string");
    }

    [Fact]
    public void Parse_DurationType_MapToString()
    {
        var help = """
            Flags:
              --timeout duration    Timeout value
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].ClrType.ShouldBe("string");
    }

    [Fact]
    public void Parse_IpSliceType_MapToMultiple()
    {
        var help = """
            Flags:
              --dns ipSlice    DNS servers
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
    }

    [Fact]
    public void Parse_UintSliceType_MapToMultiple()
    {
        var help = """
            Flags:
              --ports uintSlice    Port mappings
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
    }

    [Fact]
    public void Parse_StringSliceType_MapToMultiple()
    {
        var help = """
            Flags:
              --env stringSlice    Environment variables
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
    }

    [Fact]
    public void Parse_Int8Type_MapToInteger()
    {
        var help = """
            Flags:
              --level int8    Log level
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ClrType.ShouldBe("integer");
    }

    [Fact]
    public void Parse_Uint32Type_MapToInteger()
    {
        var help = """
            Flags:
              --size uint32    Buffer size
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ClrType.ShouldBe("integer");
    }

    [Fact]
    public void Parse_CountType_MapToInteger()
    {
        var help = """
            Flags:
              -v, --verbose count    Increase verbosity
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ClrType.ShouldBe("integer");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Parse_FlagWithAngleBracketPlaceholder()
    {
        var help = """
            Flags:
              --format <FORMAT>    Output format
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        node.Options[0].ClrType.ShouldBe("string");
    }

    [Fact]
    public void Parse_FlagWithNoDescription_LongNameOnly()
    {
        var help = """
            Flags:
              --help
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].LongName.ShouldBe("help");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
    }

    [Fact]
    public void Parse_NonIndentedLine_EndsSectionInCommands()
    {
        var help = """
            Available Commands:
              run    Run it
            Some extra text here
              stop    Should not be parsed
            """;

        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_EmptyCommandLine_NoOp()
    {
        var help = """
            Available Commands:

            Flags:
              --verbose    Be verbose
            """;

        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.ShouldBeEmpty();
        node.Options.Count.ShouldBe(1);
    }
}

// ── ArgparseHelpParser Additional Coverage ───────────────────────────────────

public class ArgparseHelpParserAdditionalTests
{
    private readonly ArgparseHelpParser _parser = new();

    [Fact]
    public void Parse_SubcommandsHeader()
    {
        var help = """
            subcommands:
              up    Start services
              down  Stop services
            """;

        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_UsageSection_SkipsUsageContent()
    {
        var help = """
            A test tool

            usage: tool [-h] [--verbose]

            options:
              --verbose    Be verbose
            """;

        var node = _parser.Parse(help, "tool");
        node.ShouldNotBeNull();
        node!.Description.ShouldBe("A test tool");
        node.Options.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_NonIndentedLineInNoneSection_CapturesDescription()
    {
        var help = """
            My Tool Description

            options:
              --verbose    Be verbose
            """;

        var node = _parser.Parse(help, "tool");
        node!.Description.ShouldBe("My Tool Description");
    }

    [Fact]
    public void Parse_PositionalArgument_BraceListSkipped_InCommands()
    {
        // In command: section, {cmd1,...} lines are skipped
        var help = """
            command:
              {up,down,ps}
                up    Start services
                down  Stop services
            """;

        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(2);
        node.SubCommands[0].Name.ShouldBe("up");
    }

    [Fact]
    public void Parse_OptionContinuation_EmptyLine_FlushesOption()
    {
        var help = """
            options:
              --verbose    Be verbose. This is a
                           long description.

              --quiet      Be quiet.
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(2);
        node.Options[0].Description!.ShouldContain("long description");
    }

    [Fact]
    public void Parse_ShortFlagWithMetavar_NoLongFlag()
    {
        // When short flag has a metavar but no long flag, the short flag's metavar
        // is consumed but without a long flag, value kind stays Flag
        var help = """
            options:
              -n NUM               Number of items
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ShortName.ShouldBe("n");
        node.Options[0].LongName.ShouldBe("n");
        // The metavar is consumed as a short-flag metavar; with no long flag and
        // no remaining metavar tokens, valueKind is Flag
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
    }

    [Fact]
    public void Parse_PositionalArgument_Required()
    {
        var help = """
            positional arguments:
              <input>              Input file
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Arguments.Count.ShouldBe(1);
        node.Arguments[0].Name.ShouldBe("input");
        node.Arguments[0].IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void Parse_PositionalArgument_Optional()
    {
        var help = """
            positional arguments:
              [output]             Output file
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Arguments.Count.ShouldBe(1);
        node.Arguments[0].IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void Parse_PositionalArgument_Variadic()
    {
        var help = """
            positional arguments:
              [files ...]          Input files
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Arguments.Count.ShouldBe(1);
        node.Arguments[0].IsVariadic.ShouldBeTrue();
        node.Arguments[0].IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void Parse_CommandLine_BraceListSkipped()
    {
        var help = """
            command:
              {up,down,ps}
                up    Start services
            """;

        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(1);
        node.SubCommands[0].Name.ShouldBe("up");
    }

    [Fact]
    public void Parse_OptionWithSeparateCommaToken()
    {
        var help = """
            options:
              -f , --file FILE     Specify a file
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ShortName.ShouldBe("f");
        node.Options[0].LongName.ShouldBe("file");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
    }
}

// ── StandardHelpParser Additional Coverage ───────────────────────────────────

public class StandardHelpParserAdditionalTests
{
    private readonly StandardHelpParser _parser = new();

    [Fact]
    public void IsValidOptionNameChar_ValidChars()
    {
        StandardHelpParser.IsValidOptionNameChar('a').ShouldBeTrue();
        StandardHelpParser.IsValidOptionNameChar('Z').ShouldBeTrue();
        StandardHelpParser.IsValidOptionNameChar('0').ShouldBeTrue();
        StandardHelpParser.IsValidOptionNameChar('-').ShouldBeTrue();
        StandardHelpParser.IsValidOptionNameChar('_').ShouldBeTrue();
        StandardHelpParser.IsValidOptionNameChar('[').ShouldBeTrue();
        StandardHelpParser.IsValidOptionNameChar(']').ShouldBeTrue();
    }

    [Fact]
    public void IsValidOptionNameChar_InvalidChars()
    {
        StandardHelpParser.IsValidOptionNameChar(' ').ShouldBeFalse();
        StandardHelpParser.IsValidOptionNameChar('=').ShouldBeFalse();
        StandardHelpParser.IsValidOptionNameChar('<').ShouldBeFalse();
    }

    [Fact]
    public void Parse_OptionShortOnly_WithSpaceBeforeLong()
    {
        var help = """
            Options:
              -f, --file PATH    File path
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ShortName.ShouldBe("f");
        node.Options[0].LongName.ShouldBe("file");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Parse_OptionLongOnly_EndOfLineNoValue()
    {
        var help = """
            Options:
              --verbose
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].LongName.ShouldBe("verbose");
        node.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
    }

    [Fact]
    public void Parse_UsageSection_ContentSkipped()
    {
        var help = """
            Usage:
              tool [options] [command]

            Commands:
              run    Run it
            """;

        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_ArgumentLine_VariadicOptional()
    {
        var help = """
            Arguments:
              [files...]    Optional files
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Arguments.Count.ShouldBe(1);
        node.Arguments[0].Name.ShouldBe("files");
        node.Arguments[0].IsVariadic.ShouldBeTrue();
        node.Arguments[0].IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void Parse_ArgumentLine_RequiredBrackets()
    {
        var help = """
            Arguments:
              <input>    Input file
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Arguments[0].IsRequired.ShouldBeTrue();
        node.Arguments[0].Name.ShouldBe("input");
    }
}

// ── PackerHelpParser Additional Coverage ─────────────────────────────────────

public class PackerHelpParserAdditionalTests
{
    private readonly PackerHelpParser _parser = new();

    [Fact]
    public void Parse_CommandsHeaderExactLength()
    {
        // "Commands" with exactly 8 characters, no colon
        var help = "Commands\n  build    Build images\n";
        var node = _parser.Parse(help, "packer");
        node!.SubCommands.Count.ShouldBe(1);
    }

    [Fact]
    public void Parse_GoFlag_SpacedPlaceholder_NoDescription()
    {
        var builder = new CommandNodeBuilder("test");
        PackerHelpParser.ParseGoOptionLine("-output PATH", builder);
        builder.Options.Count.ShouldBe(1);
        builder.Options[0].LongName.ShouldBe("output");
        builder.Options[0].ValueKind.ShouldBe(OptionValueKind.Single);
        builder.Options[0].Description.ShouldBeNull();
    }

    [Fact]
    public void Parse_GoFlag_DescriptionOnly_NoPlaceholder()
    {
        var builder = new CommandNodeBuilder("test");
        PackerHelpParser.ParseGoOptionLine("-verbose Enable verbose mode", builder);
        builder.Options.Count.ShouldBe(1);
        builder.Options[0].LongName.ShouldBe("verbose");
        builder.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
        builder.Options[0].Description.ShouldBe("Enable verbose mode");
    }

    [Fact]
    public void Parse_GoFlag_EmptyName_Skipped()
    {
        var builder = new CommandNodeBuilder("test");
        PackerHelpParser.ParseGoOptionLine("- ", builder);
        builder.Options.ShouldBeEmpty();
    }
}

// ── HelpParsers Registry Additional Coverage ─────────────────────────────────

public class HelpParsersAdditionalTests
{
    [Fact]
    public void Create_Gh_ReturnsGhStyleHelpParser()
    {
        var parser = HelpParsers.Create("gh");
        parser.ShouldBeOfType<GhStyleHelpParser>();
    }

    [Fact]
    public void KnownStrategies_ContainsGh()
    {
        HelpParsers.KnownStrategies.ShouldContain("gh");
    }

    [Fact]
    public void Register_Overwrite_ExistingStrategy()
    {
        HelpParsers.Register("test-overwrite", () => new PackerHelpParser());
        HelpParsers.Create("test-overwrite").ShouldBeOfType<PackerHelpParser>();

        HelpParsers.Register("test-overwrite", () => new CobraHelpParser());
        HelpParsers.Create("test-overwrite").ShouldBeOfType<CobraHelpParser>();
    }
}

// ── CommandNode Additional Edge Cases ────────────────────────────────────────

public class CommandNodeAdditionalTests
{
    [Fact]
    public void FindByPath_MultiLevelNonExistent_ReturnsNull()
    {
        var root = new CommandNodeBuilder("docker")
            .AddSubCommand("container", configure: c =>
            {
                c.AddSubCommand("run");
            })
            .Build();

        root.FindByPath(["container", "nonexistent"]).ShouldBeNull();
    }

    [Fact]
    public void GetLeafCommands_MixedTreeAndLeaves()
    {
        var root = new CommandNodeBuilder("cli")
            .AddSubCommand("version") // leaf at level 1
            .AddSubCommand("container", configure: c =>
            {
                c.AddSubCommand("run"); // leaf at level 2
            })
            .Build();

        var leaves = root.GetLeafCommands().ToList();
        leaves.Count.ShouldBe(2);
        leaves[0].Path.ShouldBe(new[] { "cli", "version" });
        leaves[1].Path.ShouldBe(new[] { "cli", "container", "run" });
    }

    [Fact]
    public void IsLeaf_JsonIgnored()
    {
        // Verify that IsLeaf has [JsonIgnore] by round-tripping
        var root = new CommandNodeBuilder("tool")
            .AddSubCommand("sub")
            .Build();

        var json = CommandTreeJsonSerializer.Serialize(new CommandTree
        {
            BinaryName = "tool",
            Root = root
        });

        json.ShouldNotContain("\"isLeaf\"");
    }
}

// ── CommandTreeJsonSerializer Edge Cases ─────────────────────────────────────

public class CommandTreeJsonSerializerEdgeCaseTests
{
    [Fact]
    public void Serialize_EmptyRoot_ProducesValidJson()
    {
        var tree = new CommandTree
        {
            BinaryName = "empty",
            Root = new CommandNode { Name = "empty" }
        };

        var json = CommandTreeJsonSerializer.Serialize(tree);
        var deserialized = CommandTreeJsonSerializer.Deserialize(json);

        deserialized.ShouldNotBeNull();
        deserialized!.Root.SubCommands.ShouldBeEmpty();
        deserialized.Root.Options.ShouldBeEmpty();
        deserialized.Root.Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void Deserialize_WithMultipleValueKind_PreservesEnum()
    {
        var root = new CommandNodeBuilder("test")
            .AddOption("env", valueKind: OptionValueKind.Multiple)
            .AddOption("flag", valueKind: OptionValueKind.Flag)
            .AddOption("output", valueKind: OptionValueKind.Single)
            .Build();

        var tree = new CommandTree { BinaryName = "test", Root = root };
        var json = CommandTreeJsonSerializer.Serialize(tree);

        json.ShouldContain("\"multiple\"");
        json.ShouldContain("\"flag\"");
        json.ShouldContain("\"single\"");

        var deserialized = CommandTreeJsonSerializer.Deserialize(json);
        deserialized!.Root.Options[0].ValueKind.ShouldBe(OptionValueKind.Multiple);
        deserialized.Root.Options[1].ValueKind.ShouldBe(OptionValueKind.Flag);
        deserialized.Root.Options[2].ValueKind.ShouldBe(OptionValueKind.Single);
    }

    [Fact]
    public void Serialize_ArgumentsPreserved()
    {
        var root = new CommandNodeBuilder("test")
            .AddArgument("input", 0, "Input file", true, false)
            .AddArgument("extras", 1, "Extra args", false, true)
            .Build();

        var tree = new CommandTree { BinaryName = "test", Root = root };
        var json = CommandTreeJsonSerializer.Serialize(tree);
        var deserialized = CommandTreeJsonSerializer.Deserialize(json);

        deserialized!.Root.Arguments.Count.ShouldBe(2);
        deserialized.Root.Arguments[0].Name.ShouldBe("input");
        deserialized.Root.Arguments[0].IsRequired.ShouldBeTrue();
        deserialized.Root.Arguments[1].Name.ShouldBe("extras");
        deserialized.Root.Arguments[1].IsVariadic.ShouldBeTrue();
        deserialized.Root.Arguments[1].IsRequired.ShouldBeFalse();
    }
}

// ── MockContainerRuntime Tests ───────────────────────────────────────────────

public class MockContainerRuntimeTests
{
    [Fact]
    public async Task WithHelpText_ReturnsHelpOnRun()
    {
        var runtime = MockContainerRuntime.WithHelpText("help text here");

        var buildResult = await runtime.BuildAsync("tag", "FROM alpine");
        buildResult.ShouldBe("built");

        var runResult = await runtime.RunAsync("tag", ["cmd", "--help"]);
        runResult.ShouldBe("help text here");

        // RemoveImageAsync should not throw
        await runtime.RemoveImageAsync("tag");
    }

    [Fact]
    public async Task NoOp_ReturnsEmptyStrings()
    {
        var runtime = MockContainerRuntime.NoOp;

        var buildResult = await runtime.BuildAsync("tag", "FROM alpine");
        buildResult.ShouldBe("");

        var runResult = await runtime.RunAsync("tag", ["cmd"]);
        runResult.ShouldBe("");
    }
}

// ── NewCommand ToPascalCase Edge Cases ───────────────────────────────────────

public class NewCommandAdditionalTests
{
    [Fact]
    public void ToPascalCase_NullInput_ReturnsNull()
    {
        NewCommand.ToPascalCase(null!).ShouldBeNull();
    }

    [Fact]
    public void ToPascalCase_MultipleDelimiters()
    {
        NewCommand.ToPascalCase("my-tool_name here").ShouldBe("MyToolNameHere");
    }

    [Fact]
    public void ToPascalCase_SingleChar()
    {
        NewCommand.ToPascalCase("a").ShouldBe("A");
    }
}

// ── GhStyleHelpParser Additional Coverage ────────────────────────────────────

public class GhStyleHelpParserAdditionalTests
{
    private readonly GhStyleHelpParser _parser = new();

    [Fact]
    public void Parse_FlagShortOnly_NoLong()
    {
        var help = """
            A CLI tool.

            FLAGS
              -v   Show version.
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        node!.Options.Count.ShouldBe(1);
        node.Options[0].ShortName.ShouldBe("v");
        node.Options[0].LongName.ShouldBe("v");
    }

    [Fact]
    public void Parse_FlagNoDescription_StillParsed()
    {
        var help = """
            A CLI tool.

            FLAGS
              --verbose
            """;

        // Flags require double-space separation for description, but should still parse
        // Actually the gh-style parser requires description for content lines
        // This tests the edge case where the flag line has no double-space-separated description
        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
    }

    [Fact]
    public void Parse_CommandLine_EmptyDescription_Skipped()
    {
        // When SplitOnDoubleSpace returns null for both sides
        var help = """
            A CLI tool.

            COMMANDS
              singleword
            """;

        var node = _parser.Parse(help, "cmd");
        node.ShouldNotBeNull();
        // "singleword" has no double-space separation, so no description → skipped
        node!.SubCommands.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_MultipleCommandSections_Merged()
    {
        var help = """
            A CLI tool.

            CORE COMMANDS
              alias   Create aliases.

            OTHER COMMANDS
              version   Show version.

            ADDITIONAL COMMANDS
              config   Manage config.
            """;

        var node = _parser.Parse(help, "cmd");
        node!.SubCommands.Count.ShouldBe(3);
    }

    [Fact]
    public void Parse_PlaceholderInDescription_NotAngleBracket()
    {
        // Only <letter> placeholders trigger single-value detection
        var help = """
            A CLI tool.

            FLAGS
              --debug   Enable debug mode for the CLI.
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].ValueKind.ShouldBe(OptionValueKind.Flag);
    }

    [Fact]
    public void Parse_Default_BoolInParentheses_IsStringNotInteger()
    {
        var help = """
            A CLI tool.

            FLAGS
              --format   Output format. (json)
            """;

        var node = _parser.Parse(help, "cmd");
        node!.Options[0].DefaultValue.ShouldBe("json");
        node.Options[0].ClrType.ShouldBe("string");
    }
}

// ── ScrapePipeline UseTransformer Recursive Tests ────────────────────────────

public class ScrapePipelineTransformerRecursiveTests
{
    private sealed class DescriptionPrefixTransformer : ICommandTreeTransformer
    {
        public CommandNode TransformRoot(CommandNode root) => root;

        public CommandNode TransformCommand(string commandPath, CommandNode node) => new()
        {
            Name = node.Name,
            Description = $"[{commandPath}] {node.Description}",
            Options = node.Options,
            Arguments = node.Arguments,
            SubCommands = node.SubCommands
        };
    }

    [Fact]
    public void ApplyTransforms_TransformerAppliedRecursively()
    {
        var pipeline = new ScrapePipeline()
            .Binary("tool")
            .UseTransformer(new DescriptionPrefixTransformer());

        var tree = new CommandTree
        {
            BinaryName = "tool",
            Root = new CommandNodeBuilder("tool")
            {
                Description = "Root"
            }
            .AddSubCommand("group", "Group", c =>
            {
                c.AddSubCommand("leaf", "Leaf");
            })
            .Build()
        };

        var result = pipeline.ApplyTransforms(tree);

        result.Root.SubCommands[0].Description!.ShouldContain("[tool.group]");
        result.Root.SubCommands[0].SubCommands[0].Description!.ShouldContain("[tool.group.leaf]");
    }
}

// ── CommandNodeBuilder AddArgument Record Overload ───────────────────────────

public class CommandNodeBuilderAdditionalTests
{
    [Fact]
    public void AddArgument_Record_AddsDirectly()
    {
        var arg = new ArgumentDefinition { Name = "input", Position = 0 };
        var node = new CommandNodeBuilder("cmd")
            .AddArgument(arg)
            .Build();

        node.Arguments.Count.ShouldBe(1);
        node.Arguments[0].ShouldBe(arg);
    }

    [Fact]
    public void AddArgument_WithDefaultValue()
    {
        var node = new CommandNodeBuilder("cmd")
            .AddArgument("output", 0, "Output path", false, false, "string", "./out")
            .Build();

        node.Arguments[0].DefaultValue.ShouldBe("./out");
        node.Arguments[0].IsRequired.ShouldBeFalse();
    }
}
