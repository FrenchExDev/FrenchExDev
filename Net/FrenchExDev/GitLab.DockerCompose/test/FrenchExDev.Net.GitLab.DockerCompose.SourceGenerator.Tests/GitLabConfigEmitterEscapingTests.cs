using FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FrenchExDev.Net.GitLab.DockerCompose.SourceGenerator.Tests;

public sealed class GitLabConfigEmitterEscapingTests
{
    [Theory]
    [InlineData("{\r\nname")]
    [InlineData("quote\"and\\slash\t\0")]
    [InlineData("line\u2028paragraph\u2029")]
    public void Metadata_SpecialCharacters_ProduceValidLiteralsWithOriginalValues(string value)
    {
        var source = GitLabConfigEmitter.EmitRbMetadata("TestNs",
            [new() { Prefix = value, KeyPath = [value], ValueKind = "String" }], [value]);
        var tree = CSharpSyntaxTree.ParseText(source);

        tree.GetDiagnostics().ShouldBeEmpty();
        tree.GetRoot().DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.StringLiteralToken))
            .Select(token => token.ValueText).ShouldBe(new[] { value, value, value });
    }

    [Fact]
    public void Model_MultilineDocumentation_RemainsInsideXmlComment()
    {
        var source = GitLabConfigEmitter.EmitModelClass("TestNs", "Config", new UnifiedObjectNode(),
            [new() { Name = "Name", CSharpType = "string?", DocComment = "pgbouncer['users']['{\r\nname'] <&>\u2028end" }],
            null, null);
        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.Diagnose));

        tree.GetDiagnostics().ShouldBeEmpty();
        tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>()
            .Single().Identifier.ValueText.ShouldBe("Name");
    }
}
