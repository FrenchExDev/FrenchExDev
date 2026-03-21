namespace FrenchExDev.Net.Dsl.SourceGenerator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using FrenchExDev.Net.Dsl.SourceGenerator.Lib;
using System.Collections.Immutable;

[Generator(LanguageNames.CSharp)]
public sealed class MetamodelRegistryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var conceptClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "FrenchExDev.Net.Dsl.MetaConceptAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => ExtractConceptModel(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        var collected = conceptClasses.Collect();

        context.RegisterSourceOutput(collected, static (spc, concepts) =>
        {
            var models = concepts.ToList();
            if (models.Count == 0) return;

            var source = MetamodelRegistryEmitter.Emit(models);
            spc.AddSource("MetamodelRegistry.g.cs", source);
        });
    }

    private static ConceptEmitModel? ExtractConceptModel(
        GeneratorAttributeSyntaxContext ctx, System.Threading.CancellationToken ct)
    {
        var symbol = ctx.TargetSymbol as INamedTypeSymbol;
        if (symbol is null) return null;

        // Read [MetaConcept(typeof(...))]
        var attr = ctx.Attributes.FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == "FrenchExDev.Net.Dsl.MetaConceptAttribute");
        if (attr is null) return null;

        var conceptTypeArg = attr.ConstructorArguments.FirstOrDefault();
        if (conceptTypeArg.Value is not INamedTypeSymbol conceptType) return null;

        // Collect [MetaInherits]
        var inherits = new List<string>();
        foreach (var a in symbol.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() == "FrenchExDev.Net.Dsl.MetaInheritsAttribute")
            {
                var parentArg = a.ConstructorArguments.FirstOrDefault();
                if (parentArg.Value is INamedTypeSymbol parentType)
                {
                    // Resolve the name from the parent concept's type name
                    inherits.Add(parentType.Name.Replace("Concept", ""));
                }
            }
        }

        // Collect [MetaProperty] from properties
        var properties = new List<PropertyEmitModel>();
        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            foreach (var pa in member.GetAttributes())
            {
                if (pa.AttributeClass?.ToDisplayString() == "FrenchExDev.Net.Dsl.MetaPropertyAttribute")
                {
                    var name = pa.ConstructorArguments.Length > 0 ? pa.ConstructorArguments[0].Value?.ToString() ?? "" : "";
                    var type = pa.ConstructorArguments.Length > 1 ? pa.ConstructorArguments[1].Value?.ToString() ?? "" : "";
                    var required = pa.NamedArguments.FirstOrDefault(n => n.Key == "Required").Value.Value is true;
                    properties.Add(new PropertyEmitModel { Name = name, Type = type, Required = required });
                }
            }
        }

        // Collect [MetaConstraint]
        var constraints = new List<ConstraintEmitModel>();
        foreach (var a in symbol.GetAttributes())
        {
            if (a.AttributeClass?.ToDisplayString() == "FrenchExDev.Net.Dsl.MetaConstraintAttribute")
            {
                var cName = a.ConstructorArguments.Length > 0 ? a.ConstructorArguments[0].Value?.ToString() ?? "" : "";
                var methodName = a.ConstructorArguments.Length > 1 ? a.ConstructorArguments[1].Value?.ToString() ?? "" : "";
                var message = a.NamedArguments.FirstOrDefault(n => n.Key == "Message").Value.Value?.ToString();
                constraints.Add(new ConstraintEmitModel { Name = cName, MethodName = methodName, Message = message });
            }
        }

        // Get the concept name from the first constructor arg's type name
        var conceptName = conceptType.Name.Replace("Concept", "");

        return new ConceptEmitModel
        {
            Name = conceptName,
            AttributeTypeFullName = symbol.ToDisplayString(),
            ConceptTypeFullName = conceptType.ToDisplayString(),
            Inherits = inherits,
            Properties = properties,
            Constraints = constraints,
        };
    }
}
