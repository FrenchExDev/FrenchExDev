using FrenchExDev.Net.Packer.Bundle;

namespace FrenchExDev.Net.Packer.Bundle.Tests;

public class HclWriterTests
{
    private static string Write(Action<HclWriter> action)
    {
        var sw = new StringWriter();
        using (var writer = new HclWriter(sw))
            action(writer);
        return sw.ToString().Replace("\r\n", "\n").TrimEnd('\n');
    }

    [Fact]
    public void StringArgument_EmitsQuotedValue()
    {
        var result = Write(w => w.Argument("name", "alpine"));
        result.ShouldBe("name = \"alpine\"");
    }

    [Fact]
    public void IntArgument_EmitsUnquotedValue()
    {
        var result = Write(w => w.Argument("disk_size", 20480));
        result.ShouldBe("disk_size = 20480");
    }

    [Fact]
    public void BoolArgument_EmitsLowerCase()
    {
        var result = Write(w => w.Argument("headless", true));
        result.ShouldBe("headless = true");
    }

    [Fact]
    public void NullStringArgument_EmitsNothing()
    {
        var result = Write(w => w.Argument("name", (string?)null));
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Expression_EmitsUnquoted()
    {
        var result = Write(w => w.Expression("type", "string"));
        result.ShouldBe("type = string");
    }

    [Fact]
    public void Block_EmitsBracesAndIndent()
    {
        var result = Write(w =>
        {
            using var block = w.Block("packer");
            w.Argument("required_version", ">= 1.7.0");
        });

        result.ShouldBe(
            "packer {\n" +
            "  required_version = \">= 1.7.0\"\n" +
            "}");
    }

    [Fact]
    public void LabeledBlock_EmitsLabelsQuoted()
    {
        var result = Write(w =>
        {
            using var block = w.Block("source", "virtualbox-iso", "alpine");
            w.Argument("vm_name", "test");
        });

        result.ShouldBe(
            "source \"virtualbox-iso\" \"alpine\" {\n" +
            "  vm_name = \"test\"\n" +
            "}");
    }

    [Fact]
    public void NestedBlocks_IndentCorrectly()
    {
        var result = Write(w =>
        {
            using var outer = w.Block("packer");
            using var inner = w.Block("required_plugins");
            using var plugin = w.Block("virtualbox");
            w.Argument("version", ">= 1.1.0");
        });

        result.ShouldBe(
            "packer {\n" +
            "  required_plugins {\n" +
            "    virtualbox {\n" +
            "      version = \">= 1.1.0\"\n" +
            "    }\n" +
            "  }\n" +
            "}");
    }

    [Fact]
    public void ArgumentList_EmitsJsonLikeArray()
    {
        var result = Write(w => w.ArgumentList("scripts", new[] { "a.sh", "b.sh", "c.sh" }));

        result.ShouldBe(
            "scripts = [\n" +
            "  \"a.sh\",\n" +
            "  \"b.sh\",\n" +
            "  \"c.sh\"\n" +
            "]");
    }

    [Fact]
    public void ArgumentList_SingleItem_EmitsInline()
    {
        var result = Write(w => w.ArgumentList("tags", new[] { "latest" }));
        result.ShouldBe("tags = [\"latest\"]");
    }

    [Fact]
    public void ArgumentMap_EmitsMapBlock()
    {
        var result = Write(w => w.ArgumentMap("tags", new Dictionary<string, string>
        {
            ["env"] = "dev",
            ["app"] = "test"
        }));

        result.ShouldContain("tags = {");
        result.ShouldContain("env = \"dev\"");
        result.ShouldContain("app = \"test\"");
        result.ShouldContain("}");
    }

    [Fact]
    public void ArgumentListOfLists_EmitsNestedArrays()
    {
        var commands = new List<IReadOnlyList<string>>
        {
            new[] { "modifyvm", "{{ .Name }}", "--memory", "256" },
            new[] { "modifyvm", "{{ .Name }}", "--cpus", "4" }
        };

        var result = Write(w => w.ArgumentListOfLists("vboxmanage", commands));

        result.ShouldContain("vboxmanage = [");
        result.ShouldContain("[\"modifyvm\", \"{{ .Name }}\", \"--memory\", \"256\"],");
        result.ShouldContain("[\"modifyvm\", \"{{ .Name }}\", \"--cpus\", \"4\"]");
        result.ShouldContain("]");
    }

    [Fact]
    public void Comment_EmitsDoubleSlash()
    {
        var result = Write(w => w.Comment("This is a comment"));
        result.ShouldBe("// This is a comment");
    }

    [Fact]
    public void EscapesSpecialCharacters()
    {
        var result = Write(w => w.Argument("cmd", "echo \"hello\"\nnewline"));
        result.ShouldBe("cmd = \"echo \\\"hello\\\"\\nnewline\"");
    }
}
