namespace FrenchExDev.Net.Requirements.SourceGenerator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using FrenchExDev.Net.Requirements.SourceGenerator.Lib;

[Generator(LanguageNames.CSharp)]
public sealed class RequirementRegistryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax cds && cds.Modifiers.Any(m => m.Text == "abstract"),
                transform: static (ctx, ct) => ExtractRequirement(ctx, ct))
            .Where(static m => m != null)
            .Select(static (m, _) => m!);

        var collected = classes.Collect();

        context.RegisterSourceOutput(collected, static (spc, reqs) =>
        {
            var models = reqs.ToList();
            if (models.Count == 0) return;
            var source = RequirementRegistryEmitter.Emit(models);
            spc.AddSource("RequirementRegistry.g.cs", source);
        });
    }

    private static RequirementEmitModel? ExtractRequirement(GeneratorSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) as INamedTypeSymbol;
        if (symbol == null || !symbol.IsAbstract) return null;

        var baseType = symbol.BaseType;
        string? kind = null;
        string? parentTypeFullName = null;

        while (baseType != null)
        {
            var baseFullName = baseType.OriginalDefinition.ToDisplayString();

            if (baseFullName.StartsWith("FrenchExDev.Net.Requirements.Epic"))
            { kind = "Epic"; break; }
            if (baseFullName.StartsWith("FrenchExDev.Net.Requirements.Feature<"))
            { kind = "Feature"; parentTypeFullName = baseType.TypeArguments[0].ToDisplayString(); break; }
            if (baseFullName.StartsWith("FrenchExDev.Net.Requirements.Feature"))
            { kind = "Feature"; break; }
            if (baseFullName.StartsWith("FrenchExDev.Net.Requirements.Story<"))
            { kind = "Story"; parentTypeFullName = baseType.TypeArguments[0].ToDisplayString(); break; }
            if (baseFullName.StartsWith("FrenchExDev.Net.Requirements.RequirementTask<"))
            { kind = "Task"; parentTypeFullName = baseType.TypeArguments[0].ToDisplayString(); break; }
            if (baseFullName.StartsWith("FrenchExDev.Net.Requirements.Bug"))
            { kind = "Bug"; break; }

            baseType = baseType.BaseType;
        }

        if (kind == null) return null;

        // Find acceptance criteria: abstract methods returning AcceptanceCriterionResult
        var acs = new List<string>();
        foreach (var member in symbol.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.IsAbstract && member.ReturnType.Name == "AcceptanceCriterionResult")
            {
                acs.Add(member.Name);
            }
        }

        // Get Title from override
        string title = symbol.Name;
        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.Name == "Title" && member.IsOverride)
            {
                // Try to extract string literal from getter
                var syntaxRef = member.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxRef != null)
                {
                    var propSyntax = syntaxRef.GetSyntax(ct) as PropertyDeclarationSyntax;
                    if (propSyntax?.ExpressionBody?.Expression is LiteralExpressionSyntax literal)
                    {
                        title = literal.Token.ValueText;
                    }
                }
            }
        }

        return new RequirementEmitModel
        {
            TypeFullName = symbol.ToDisplayString(),
            Kind = kind,
            Title = title,
            ParentTypeFullName = parentTypeFullName,
            AcceptanceCriteria = acs,
        };
    }
}
