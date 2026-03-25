using FrenchExDev.Net.FiniteStateMachine.SourceGenerator.Lib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace FrenchExDev.Net.FiniteStateMachine.SourceGenerator;

[Generator]
public sealed class FiniteStateMachineGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "FrenchExDev.Net.FiniteStateMachine.Attributes.StateMachineAttribute";
    private const string TransitionAttributeFullName = "FrenchExDev.Net.FiniteStateMachine.Attributes.TransitionAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetModel(ctx, ct))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(models, static (ctx, model) =>
        {
            var source = StateMachineEmitter.Emit(model!);
            ctx.AddSource($"{model!.ClassName}.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static StateMachineEmitModel? GetModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol)
            return null;

        ct.ThrowIfCancellationRequested();

        var attr = ctx.Attributes[0];
        var (stateEnumType, eventEnumType, initialState) = ReadStateMachineAttribute(attr);
        if (stateEnumType == null || eventEnumType == null || string.IsNullOrEmpty(initialState))
            return null;

        var stateEnumFull = stateEnumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var eventEnumFull = eventEnumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var stateMembers = CollectEnumMembers(stateEnumType);
        var eventMembers = CollectEnumMembers(eventEnumType);
        var transitions = CollectTransitions(classSymbol, stateEnumType, eventEnumType, ct);
        var userMethods = CollectUserDefinedMethods(classSymbol);

        var ns = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } nsSym
            ? nsSym.ToDisplayString()
            : string.Empty;

        return new StateMachineEmitModel(
            ns,
            classSymbol.Name,
            stateEnumFull,
            eventEnumFull,
            initialState,
            stateMembers,
            eventMembers,
            transitions,
            userMethods);
    }

    private static (INamedTypeSymbol? stateEnum, INamedTypeSymbol? eventEnum, string initialState) ReadStateMachineAttribute(AttributeData attr)
    {
        if (attr.ConstructorArguments.Length < 2)
            return (null, null, "");

        var stateEnumType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
        var eventEnumType = attr.ConstructorArguments[1].Value as INamedTypeSymbol;

        string initialState = "";
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == "InitialState" && arg.Value.Value is string s)
                initialState = s;
        }

        return (stateEnumType, eventEnumType, initialState);
    }

    private static List<string> CollectEnumMembers(INamedTypeSymbol enumType)
    {
        return enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .Select(f => f.Name)
            .ToList();
    }

    private static List<TransitionEmitModel> CollectTransitions(
        INamedTypeSymbol classSymbol,
        INamedTypeSymbol stateEnumType,
        INamedTypeSymbol eventEnumType,
        CancellationToken ct)
    {
        var transitions = new List<TransitionEmitModel>();
        foreach (var member in classSymbol.GetMembers())
        {
            ct.ThrowIfCancellationRequested();
            if (member is not IMethodSymbol method || method.Name != "DefineTransitions")
                continue;

            foreach (var transAttr in method.GetAttributes())
            {
                var model = TryParseTransitionAttribute(transAttr, stateEnumType, eventEnumType);
                if (model != null)
                    transitions.Add(model);
            }
        }

        return transitions;
    }

    private static TransitionEmitModel? TryParseTransitionAttribute(
        AttributeData transAttr,
        INamedTypeSymbol stateEnumType,
        INamedTypeSymbol eventEnumType)
    {
        if (transAttr.AttributeClass?.ToDisplayString() != TransitionAttributeFullName)
            return null;

        if (transAttr.ConstructorArguments.Length < 3)
            return null;

        var fromName = GetEnumMemberName(stateEnumType, transAttr.ConstructorArguments[0]);
        var eventName = GetEnumMemberName(eventEnumType, transAttr.ConstructorArguments[1]);
        var toName = GetEnumMemberName(stateEnumType, transAttr.ConstructorArguments[2]);

        if (fromName == null || eventName == null || toName == null)
            return null;

        string? guardMethod = null;
        foreach (var namedArg in transAttr.NamedArguments)
        {
            if (namedArg.Key == "Guard" && namedArg.Value.Value is string g)
                guardMethod = g;
        }

        return new TransitionEmitModel(fromName, eventName, toName, guardMethod);
    }

    private static List<string> CollectUserDefinedMethods(INamedTypeSymbol classSymbol)
    {
        var userMethods = new List<string>();
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary
                && method.Name != "DefineTransitions")
            {
                userMethods.Add(method.Name);
            }
        }

        return userMethods;
    }

    private static string? GetEnumMemberName(INamedTypeSymbol enumType, TypedConstant value)
    {
        if (value.Value == null) return null;

        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue && Equals(member.ConstantValue, value.Value))
                return member.Name;
        }

        return null;
    }
}
