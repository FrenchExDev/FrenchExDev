using FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator;

namespace FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator.Tests;

public class GitLabRbParserTests
{
    [Fact]
    public void Parse_SimpleSettings_ExtractsPrefix()
    {
        var content = """
            # nginx['listen_port'] = 80
            # nginx['listen_https'] = false
            """;

        var model = GitLabRbParser.Parse(content, "18.10.1");

        model.PrefixGroups.Count.ShouldBe(1);
        model.PrefixGroups[0].Prefix.ShouldBe("nginx");
        model.PrefixGroups[0].Root.Children.Count.ShouldBe(2);
        model.PrefixGroups[0].Root.Children["listen_port"].LeafType.ShouldBe(GitLabRbValueType.Integer);
        model.PrefixGroups[0].Root.Children["listen_https"].LeafType.ShouldBe(GitLabRbValueType.Boolean);
    }

    [Fact]
    public void Parse_NestedBrackets_CreatesHierarchy()
    {
        var content = """
            # gitlab_rails['object_store']['enabled'] = false
            # gitlab_rails['object_store']['connection'] = {}
            # gitlab_rails['object_store']['objects']['artifacts']['bucket'] = nil
            """;

        var model = GitLabRbParser.Parse(content, "18.0.0");

        var rails = model.PrefixGroups[0];
        rails.Prefix.ShouldBe("gitlab_rails");

        var objectStore = rails.Root.Children["object_store"];
        objectStore.Children.ContainsKey("enabled").ShouldBeTrue();
        objectStore.Children["enabled"].LeafType.ShouldBe(GitLabRbValueType.Boolean);
        objectStore.Children["connection"].LeafType.ShouldBe(GitLabRbValueType.StringDict);

        var objects = objectStore.Children["objects"];
        var artifacts = objects.Children["artifacts"];
        artifacts.Children["bucket"].LeafType.ShouldBe(GitLabRbValueType.Nil);
    }

    [Fact]
    public void Parse_StandaloneUrl_BothActiveAndCommentedAreDeclarations()
    {
        // In gitlab.rb.template, ALL lines are declarations of available settings.
        // The uncommented `external_url` is the only active one, but commented
        // `# registry_external_url` is still a valid setting declaration.
        // The SG extracts the full configuration surface, not just active values.
        var content = """
            external_url 'GENERATED_EXTERNAL_URL'
            # registry_external_url 'https://registry.example.com'
            """;

        var model = GitLabRbParser.Parse(content, "18.0.0");

        model.StandaloneUrls.Count.ShouldBe(2);
        model.StandaloneUrls[0].RubyKey.ShouldBe("external_url");
        model.StandaloneUrls[1].RubyKey.ShouldBe("registry_external_url");
    }

    [Fact]
    public void Parse_DocComment_AttachedToSetting()
    {
        var content = """
            ###! Tells the rails application how long it has to complete a request
            # gitlab_rails['max_request_duration_seconds'] = 57
            """;

        var model = GitLabRbParser.Parse(content, "18.0.0");

        var setting = model.PrefixGroups[0].Root.Children["max_request_duration_seconds"];
        setting.DocComment.ShouldNotBeNull();
        setting.DocComment.ShouldContain("Tells the rails application");
    }

    [Fact]
    public void Parse_MultiLineHash_ParsedAsSubTree()
    {
        var content = """
            # nginx['proxy_set_headers'] = {
            #  "Host" => "$http_host_with_default",
            #  "X-Real-IP" => "$remote_addr",
            # }
            """;

        var model = GitLabRbParser.Parse(content, "18.0.0");

        var headers = model.PrefixGroups[0].Root.Children["proxy_set_headers"];
        // Flat string dict — should be a leaf
        headers.LeafType.ShouldBe(GitLabRbValueType.StringDict);
    }

    [Fact]
    public void Parse_SymbolKeyHash_CreatesSubTree()
    {
        var content = """
            # gitaly['configuration'] = {
            #   listen_addr: 'localhost:8075',
            #   auth: {
            #     token: '<secret>',
            #     transitioning: false,
            #   },
            #   storage: [
            #     {
            #       name: 'default',
            #       path: '/var/opt/gitlab/git-data/repositories',
            #     },
            #   ],
            # }
            """;

        var model = GitLabRbParser.Parse(content, "18.0.0");

        var gitaly = model.PrefixGroups[0];
        gitaly.Prefix.ShouldBe("gitaly");

        var config = gitaly.Root.Children["configuration"];
        config.Children.ContainsKey("listen_addr").ShouldBeTrue();
        config.Children["listen_addr"].LeafType.ShouldBe(GitLabRbValueType.String);

        var auth = config.Children["auth"];
        auth.Children["token"].LeafType.ShouldBe(GitLabRbValueType.String);
        auth.Children["transitioning"].LeafType.ShouldBe(GitLabRbValueType.Boolean);

        var storage = config.Children["storage"];
        storage.IsArrayOfObjects.ShouldBeTrue();
        storage.Children.ContainsKey("name").ShouldBeTrue();
        storage.Children.ContainsKey("path").ShouldBeTrue();
    }

    [Fact]
    public void Parse_InlineComment_Stripped()
    {
        var content = "# gitlab_rails['enable'] = true # do not disable";

        var model = GitLabRbParser.Parse(content, "18.0.0");

        var setting = model.PrefixGroups[0].Root.Children["enable"];
        setting.LeafType.ShouldBe(GitLabRbValueType.Boolean);
        setting.ExampleValue.ShouldBe("true");
    }

    [Fact]
    public void Parse_MultiplePrefixes_GroupedCorrectly()
    {
        var content = """
            # nginx['enable'] = true
            # redis['enable'] = true
            # nginx['listen_port'] = 80
            # puma['worker_processes'] = 2
            """;

        var model = GitLabRbParser.Parse(content, "18.0.0");

        model.PrefixGroups.Count.ShouldBe(3);
        var prefixes = model.PrefixGroups.Select(g => g.Prefix).OrderBy(p => p).ToList();
        prefixes.ShouldContain("nginx");
        prefixes.ShouldContain("redis");
        prefixes.ShouldContain("puma");
    }

    [Fact]
    public void InferValueType_CorrectlyClassifies()
    {
        GitLabRbParser.InferValueType("true").ShouldBe(GitLabRbValueType.Boolean);
        GitLabRbParser.InferValueType("false").ShouldBe(GitLabRbValueType.Boolean);
        GitLabRbParser.InferValueType("nil").ShouldBe(GitLabRbValueType.Nil);
        GitLabRbParser.InferValueType("80").ShouldBe(GitLabRbValueType.Integer);
        GitLabRbParser.InferValueType("5432").ShouldBe(GitLabRbValueType.Integer);
        GitLabRbParser.InferValueType("17179869184").ShouldBe(GitLabRbValueType.Long);
        GitLabRbParser.InferValueType("0.9").ShouldBe(GitLabRbValueType.Float);
        GitLabRbParser.InferValueType("'value'").ShouldBe(GitLabRbValueType.String);
        GitLabRbParser.InferValueType("\"value\"").ShouldBe(GitLabRbValueType.String);
        GitLabRbParser.InferValueType("[]").ShouldBe(GitLabRbValueType.StringList);
        GitLabRbParser.InferValueType("{}").ShouldBe(GitLabRbValueType.StringDict);
    }

    [Fact]
    public void ExtractVersion_FromFilename()
    {
        GitLabRbParser.ExtractVersion("gitlab-18.10.1.rb").ShouldBe("18.10.1");
        GitLabRbParser.ExtractVersion("gitlab-7.13.5.rb").ShouldBe("7.13.5");
    }

    [Fact]
    public void Parse_RealFile_HasExpectedPrefixes()
    {
        var resourceDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "resources"));
        var files = Directory.GetFiles(resourceDir, "gitlab-*.rb");
        if (files.Length == 0) return; // skip if no files downloaded

        // Parse the latest version by semantic version
        var latest = files
            .Select(f => (File: f, Version: GitLabRbParser.ExtractVersion(Path.GetFileName(f))))
            .OrderByDescending(x => x.Version, Comparer<string>.Create((a, b) =>
            {
                var pa = a.Split('.'); var pb = b.Split('.');
                for (int j = 0; j < Math.Max(pa.Length, pb.Length); j++)
                {
                    var va = j < pa.Length && int.TryParse(pa[j], out var ia) ? ia : 0;
                    var vb = j < pb.Length && int.TryParse(pb[j], out var ib) ? ib : 0;
                    if (va != vb) return va.CompareTo(vb);
                }
                return 0;
            }))
            .First().File;
        var content = File.ReadAllText(latest);
        var version = GitLabRbParser.ExtractVersion(Path.GetFileName(latest));

        var model = GitLabRbParser.Parse(content, version);

        // Should have many prefix groups
        model.PrefixGroups.Count.ShouldBeGreaterThan(30);

        // Should have standalone URLs
        model.StandaloneUrls.Count.ShouldBeGreaterThan(3);

        // Known prefixes should exist
        var prefixes = model.PrefixGroups.Select(g => g.Prefix).ToHashSet();
        prefixes.ShouldContain("nginx");
        prefixes.ShouldContain("gitlab_rails");
        prefixes.ShouldContain("redis");
        prefixes.ShouldContain("postgresql");
        prefixes.ShouldContain("puma");
        prefixes.ShouldContain("sidekiq");
        prefixes.ShouldContain("gitaly");
        prefixes.ShouldContain("registry");
        prefixes.ShouldContain("prometheus");

        // gitlab_rails should have many settings
        var rails = model.PrefixGroups.First(g => g.Prefix == "gitlab_rails");
        CountLeaves(rails.Root).ShouldBeGreaterThan(100);
    }

    private static int CountLeaves(GitLabRbObjectNode node)
    {
        if (node.LeafType != null) return 1;
        int count = 0;
        foreach (var child in node.Children.Values)
            count += CountLeaves(child);
        return count;
    }
}
