using FrenchExDev.Net.Injectable.SourceGenerator.Lib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace FrenchExDev.Net.Injectable.DryIoc.SourceGenerator;

[Generator]
public sealed class DryIocInjectableGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "FrenchExDev.Net.Injectable.Attributes.InjectableAttribute";
    private const string DecoratorAttributeFullName = "FrenchExDev.Net.Injectable.Attributes.InjectableDecoratorAttribute";
    private const string DefaultsAttributeFullName = "FrenchExDev.Net.Injectable.Attributes.InjectableDefaultsAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classServices = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetServiceModels(ctx, ct))
            .Where(static m => m is not null)
            .SelectMany(static (models, _) => models!);

        var interfaceContracts = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, ct) => GetInterfaceContract(ctx, ct))
            .Where(static m => m is not null);

        var decorators = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                DecoratorAttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetDecoratorModel(ctx, ct))
            .Where(static m => m is not null);

        var assemblyDefaults = context.CompilationProvider
            .Select(static (compilation, _) => ReadAssemblyDefaults(compilation));

        var combined = context.CompilationProvider
            .Select(static (c, _) => c.AssemblyName ?? "Assembly")
            .Combine(classServices.Collect())
            .Combine(decorators.Collect())
            .Combine(assemblyDefaults)
            .Combine(interfaceContracts.Collect())
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (ctx, tuple) =>
        {
            var (((((name, classModels), decoratorModels), defaultScope), contracts), compilation) = tuple;
            EmitSource(ctx, name, classModels, decoratorModels, defaultScope, contracts, compilation);
        });
    }

    // ── Source output orchestration ─────────────────────────────────

    private static void EmitSource(
        SourceProductionContext ctx,
        string name,
        ImmutableArray<InjectableServiceModel> classModels,
        ImmutableArray<InjectableDecoratorModel?> decoratorModels,
        string? defaultScope,
        ImmutableArray<InterfaceContractModel?> contracts,
        Compilation compilation)
    {
        var validServices = CollectExplicitServices(classModels, defaultScope, out var explicitTypes);
        AddContractInferredServices(validServices, explicitTypes, contracts, compilation, ctx.CancellationToken);

        var validDecorators = CollectDecorators(decoratorModels);

        if (validServices.Count == 0 && validDecorators.Count == 0)
            return;

        var emitModel = new InjectableEmitModel(name, validServices, validDecorators);
        var source = InjectableEmitter.EmitDryIoc(emitModel);
        ctx.AddSource("InjectableExtensions.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static List<InjectableServiceModel> CollectExplicitServices(
        ImmutableArray<InjectableServiceModel> classModels,
        string? defaultScope,
        out HashSet<string> explicitTypes)
    {
        var result = new List<InjectableServiceModel>();
        explicitTypes = new HashSet<string>();

        foreach (var m in classModels)
        {
            explicitTypes.Add(m.ImplementationTypeFull);

            if (defaultScope is not null && !m.HasExplicitScope())
            {
                result.Add(new InjectableServiceModel(
                    m.ImplementationTypeFull, m.ServiceTypesFull, defaultScope,
                    m.Key, m.TryAdd, m.IsOpenGeneric));
            }
            else
            {
                result.Add(m);
            }
        }

        return result;
    }

    private static void AddContractInferredServices(
        List<InjectableServiceModel> services,
        HashSet<string> explicitTypes,
        ImmutableArray<InterfaceContractModel?> contracts,
        Compilation compilation,
        CancellationToken ct)
    {
        foreach (var contract in contracts)
        {
            if (contract is null) continue;

            var implementors = FindImplementors(compilation, contract.InterfaceSymbolName, ct);
            foreach (var implClass in implementors)
            {
                var implFull = implClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (explicitTypes.Contains(implFull) || implClass.IsAbstract)
                    continue;

                var isOpenGeneric = implClass.IsGenericType && implClass.TypeParameters.Length > 0;
                if (isOpenGeneric)
                    implFull = ToUnboundGeneric(implClass);

                var serviceTypes = new List<string> { contract.InterfaceTypeFull };
                services.Add(new InjectableServiceModel(
                    implFull, serviceTypes, contract.Scope,
                    contract.Key, contract.TryAdd, isOpenGeneric, scopeIsExplicit: true));
            }
        }
    }

    private static List<InjectableDecoratorModel> CollectDecorators(ImmutableArray<InjectableDecoratorModel?> decoratorModels)
    {
        var result = new List<InjectableDecoratorModel>();
        foreach (var d in decoratorModels)
        {
            if (d is not null)
                result.Add(d);
        }
        return result;
    }

    // ── Assembly defaults ───────────────────────────────────────────

    private static string? ReadAssemblyDefaults(Compilation compilation)
    {
        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != DefaultsAttributeFullName)
                continue;

            foreach (var arg in attr.NamedArguments)
            {
                if (arg.Key == "Scope" && arg.Value.Value is int scopeInt)
                    return ScopeIntToString(scopeInt);
            }
        }
        return null;
    }

    // ── Interface contract extraction ───────────────────────────────

    private static InterfaceContractModel? GetInterfaceContract(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol ifaceSymbol)
            return null;

        ct.ThrowIfCancellationRequested();

        var attr = ctx.Attributes[0];
        var (scope, _, key, tryAdd, _) = ReadAttributeArgs(attr);

        var isOpenGeneric = ifaceSymbol.IsGenericType && ifaceSymbol.TypeParameters.Length > 0;
        var ifaceFull = isOpenGeneric
            ? ToUnboundGeneric(ifaceSymbol)
            : ifaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new InterfaceContractModel(
            ifaceSymbol.ToDisplayString(), ifaceFull, scope, key, tryAdd, isOpenGeneric);
    }

    // ── Find implementors ───────────────────────────────────────────

    private static List<INamedTypeSymbol> FindImplementors(Compilation compilation, string interfaceFullName, CancellationToken ct)
    {
        var results = new List<INamedTypeSymbol>();
        var stack = new Stack<INamespaceOrTypeSymbol>();
        stack.Push(compilation.Assembly.GlobalNamespace);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = stack.Pop();

            if (current is INamedTypeSymbol type && type.TypeKind == TypeKind.Class && !type.IsAbstract)
                CheckTypeForInterface(type, interfaceFullName, results);

            PushChildren(current, stack);
        }

        return results;
    }

    private static void CheckTypeForInterface(INamedTypeSymbol type, string interfaceFullName, List<INamedTypeSymbol> results)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString() == interfaceFullName ||
                (iface.IsGenericType && iface.OriginalDefinition.ToDisplayString() == interfaceFullName))
            {
                results.Add(type);
                return;
            }
        }
    }

    private static void PushChildren(INamespaceOrTypeSymbol current, Stack<INamespaceOrTypeSymbol> stack)
    {
        if (current is INamespaceSymbol ns)
        {
            foreach (var member in ns.GetMembers())
                stack.Push(member);
        }
        else if (current is INamedTypeSymbol parentType)
        {
            foreach (var nested in parentType.GetTypeMembers())
                stack.Push(nested);
        }
    }

    // ── Class-level service model extraction ────────────────────────

    private static IEnumerable<InjectableServiceModel>? GetServiceModels(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol)
            return null;

        ct.ThrowIfCancellationRequested();

        if (classSymbol.IsAbstract)
            return null;

        var isOpenGeneric = classSymbol.IsGenericType && classSymbol.TypeParameters.Length > 0;
        var implFull = isOpenGeneric
            ? ToUnboundGeneric(classSymbol)
            : classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var results = new List<InjectableServiceModel>();
        foreach (var attr in ctx.Attributes)
        {
            var (scope, asTypes, key, tryAdd, scopeIsExplicit) = ReadAttributeArgs(attr);
            var serviceTypes = ResolveServiceTypes(classSymbol, asTypes, isOpenGeneric);
            results.Add(new InjectableServiceModel(implFull, serviceTypes, scope, key, tryAdd, isOpenGeneric, scopeIsExplicit));
        }

        return results;
    }

    private static List<string> ResolveServiceTypes(INamedTypeSymbol classSymbol, List<string>? asTypes, bool isOpenGeneric)
    {
        if (asTypes is not null)
            return asTypes;

        var serviceTypes = new List<string>();
        foreach (var iface in classSymbol.Interfaces)
        {
            var ifaceFull = isOpenGeneric && iface.IsGenericType
                ? ToUnboundGeneric(iface)
                : iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            serviceTypes.Add(ifaceFull);
        }
        return serviceTypes;
    }

    // ── Decorator extraction ────────────────────────────────────────

    private static InjectableDecoratorModel? GetDecoratorModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol)
            return null;

        ct.ThrowIfCancellationRequested();

        var attr = ctx.Attributes[0];
        var decoratorFull = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not INamedTypeSymbol serviceSymbol)
            return null;

        var serviceTypeFull = serviceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        int order = 0;

        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == "Order" && arg.Value.Value is int orderVal)
                order = orderVal;
        }

        return new InjectableDecoratorModel(decoratorFull, serviceTypeFull, order);
    }

    // ── Attribute reading ───────────────────────────────────────────

    private static (string Scope, List<string>? AsTypes, string? Key, bool TryAdd, bool ScopeIsExplicit) ReadAttributeArgs(AttributeData attr)
    {
        var scope = "Transient";
        List<string>? asTypes = null;
        string? key = null;
        bool tryAdd = false;
        bool scopeIsExplicit = false;

        foreach (var arg in attr.NamedArguments)
        {
            switch (arg.Key)
            {
                case "Scope" when arg.Value.Value is int scopeInt:
                    scope = ScopeIntToString(scopeInt);
                    scopeIsExplicit = true;
                    break;
                case "As" when arg.Value.Kind == TypedConstantKind.Array:
                    asTypes = ParseAsTypes(arg.Value);
                    break;
                case "Key" when arg.Value.Value is string keyVal:
                    key = keyVal;
                    break;
                case "TryAdd" when arg.Value.Value is bool tryAddVal:
                    tryAdd = tryAddVal;
                    break;
            }
        }

        return (scope, asTypes, key, tryAdd, scopeIsExplicit);
    }

    private static List<string>? ParseAsTypes(TypedConstant value)
    {
        var result = new List<string>();
        foreach (var element in value.Values)
        {
            if (element.Value is INamedTypeSymbol asSymbol)
                result.Add(asSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }
        return result.Count > 0 ? result : null;
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static string ScopeIntToString(int scopeInt)
    {
        switch (scopeInt)
        {
            case 0: return "Transient";
            case 1: return "Scoped";
            case 2: return "Singleton";
            default: return "Transient";
        }
    }

    private static string ToUnboundGeneric(INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? "global::"
            : $"global::{symbol.ContainingNamespace.ToDisplayString()}.";
        var commas = new string(',', symbol.TypeParameters.Length - 1);
        return $"{ns}{symbol.Name}<{commas}>";
    }
}
