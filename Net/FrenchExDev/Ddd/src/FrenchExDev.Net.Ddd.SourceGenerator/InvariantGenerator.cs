#nullable enable
namespace FrenchExDev.Net.Ddd.SourceGenerator
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using FrenchExDev.Net.Ddd.SourceGenerator.Lib;
    using System.Collections.Generic;
    using System.Linq;

    [Generator(LanguageNames.CSharp)]
    public sealed class InvariantGenerator : IIncrementalGenerator
    {
        private const string AggregateRootAttr = "FrenchExDev.Net.Ddd.Attributes.AggregateRootAttribute";
        private const string EntityAttr = "FrenchExDev.Net.Ddd.Attributes.EntityAttribute";
        private const string InvariantAttr = "FrenchExDev.Net.Ddd.Attributes.InvariantAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find classes with [AggregateRoot] or [Entity]
            var aggregates = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    AggregateRootAttr,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, ct) => ExtractInvariantModel(ctx))
                .Where(static m => m != null)
                .Select(static (m, _) => m!);

            var entities = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    EntityAttr,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, ct) => ExtractInvariantModel(ctx))
                .Where(static m => m != null)
                .Select(static (m, _) => m!);

            context.RegisterSourceOutput(aggregates, static (spc, model) => EmitInvariant(spc, model));
            context.RegisterSourceOutput(entities, static (spc, model) => EmitInvariant(spc, model));
        }

        private static InvariantModel? ExtractInvariantModel(GeneratorAttributeSyntaxContext ctx)
        {
            var symbol = ctx.TargetSymbol as INamedTypeSymbol;
            if (symbol == null) return null;

            var invariantMethods = new List<string>();
            foreach (var member in symbol.GetMembers().OfType<IMethodSymbol>())
            {
                foreach (var attr in member.GetAttributes())
                {
                    if (attr.AttributeClass != null && attr.AttributeClass.ToDisplayString() == InvariantAttr)
                    {
                        invariantMethods.Add(member.Name);
                        break;
                    }
                }
            }

            if (invariantMethods.Count == 0) return null;

            return new InvariantModel(
                symbol.ContainingNamespace.ToDisplayString(),
                symbol.Name,
                invariantMethods);
        }

        private static void EmitInvariant(SourceProductionContext spc, InvariantModel model)
        {
            var source = InvariantEmitter.Emit(model.Namespace, model.ClassName, model.InvariantMethodNames);
            spc.AddSource(model.ClassName + ".Invariants.g.cs", source);
        }
    }

    internal sealed class InvariantModel
    {
        public string Namespace { get; }
        public string ClassName { get; }
        public List<string> InvariantMethodNames { get; }

        public InvariantModel(string ns, string className, List<string> invariantMethodNames)
        {
            Namespace = ns;
            ClassName = className;
            InvariantMethodNames = invariantMethodNames;
        }
    }
}
