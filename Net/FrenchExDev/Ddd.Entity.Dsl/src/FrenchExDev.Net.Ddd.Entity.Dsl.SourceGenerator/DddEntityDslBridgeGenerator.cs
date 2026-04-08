namespace FrenchExDev.Net.Ddd.Entity.Dsl.SourceGenerator;

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using FrenchExDev.Net.Ddd.Entity.Dsl.SourceGenerator.Lib;

/// <summary>
/// Bridge source generator: reads DDD attributes ([AggregateRoot], [Entity])
/// and emits Entity.Dsl attributes ([MappedEntity], [PrimaryKey], [NavigationProperty])
/// on partial classes.
///
/// This allows DDD users to use Entity.Dsl without manually adding Entity.Dsl attributes.
/// The Entity.Dsl SG then picks up [MappedEntity] and generates EF Core code.
/// </summary>
[Generator]
public sealed class DddEntityDslBridgeGenerator : IIncrementalGenerator
{
    private const string EntityAttributeFqn = "FrenchExDev.Net.Ddd.Attributes.EntityAttribute";
    private const string AggregateRootAttributeFqn = "FrenchExDev.Net.Ddd.Attributes.AggregateRootAttribute";
    private const string EntityIdAttributeFqn = "FrenchExDev.Net.Ddd.Attributes.EntityIdAttribute";
    private const string CompositionAttributeFqn = "FrenchExDev.Net.Ddd.Attributes.CompositionAttribute";
    private const string AggregationAttributeFqn = "FrenchExDev.Net.Ddd.Attributes.AggregationAttribute";
    private const string AssociationAttributeFqn = "FrenchExDev.Net.Ddd.Attributes.AssociationAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Discover [Entity] classes
        var entities = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                EntityAttributeFqn,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => ExtractModel(ctx, ct, isAggregateRoot: false))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // Discover [AggregateRoot] classes
        var aggregateRoots = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AggregateRootAttributeFqn,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => ExtractModel(ctx, ct, isAggregateRoot: true))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // Merge and emit
        var all = entities.Collect()
            .Combine(aggregateRoots.Collect())
            .SelectMany(static (pair, _) => pair.Left.AddRange(pair.Right));

        context.RegisterSourceOutput(all, static (spc, model) =>
        {
            var source = DddEntityDslBridgeEmitter.Emit(model);
            spc.AddSource($"{model.ClassName}.DddBridge.g.cs",
                SourceText.From(source, Encoding.UTF8));
        });
    }

    private static DddBridgeModel? ExtractModel(
        GeneratorAttributeSyntaxContext ctx,
        CancellationToken ct,
        bool isAggregateRoot)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        ct.ThrowIfCancellationRequested();

        var model = new DddBridgeModel
        {
            Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
            ClassName = typeSymbol.Name,
            IsAggregateRoot = isAggregateRoot,
            IsEntity = !isAggregateRoot
        };

        // Scan properties for DDD attributes
        foreach (var member in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            ct.ThrowIfCancellationRequested();

            if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public)
                continue;

            var attrs = member.GetAttributes();

            if (HasAttribute(attrs, EntityIdAttributeFqn))
                model.EntityIdPropertyNames.Add(member.Name);
            if (HasAttribute(attrs, CompositionAttributeFqn))
                model.CompositionPropertyNames.Add(member.Name);
            if (HasAttribute(attrs, AggregationAttributeFqn))
                model.AggregationPropertyNames.Add(member.Name);
            if (HasAttribute(attrs, AssociationAttributeFqn))
                model.AssociationPropertyNames.Add(member.Name);
        }

        return model;
    }

    private static bool HasAttribute(ImmutableArray<AttributeData> attrs, string fqn)
        => attrs.Any(a => a.AttributeClass?.ToDisplayString() == fqn);
}
