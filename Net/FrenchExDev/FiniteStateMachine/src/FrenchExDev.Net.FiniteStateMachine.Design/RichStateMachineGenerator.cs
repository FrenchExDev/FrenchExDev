using FrenchExDev.Net.FiniteStateMachine.SourceGenerator.Lib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace FrenchExDev.Net.FiniteStateMachine.Design;

[Generator]
public sealed class RichStateMachineGenerator : IIncrementalGenerator
{
    private const string RichSmAttr = "FrenchExDev.Net.FiniteStateMachine.Attributes.RichStateMachineAttribute";
    private const string StateAttr = "FrenchExDev.Net.FiniteStateMachine.Attributes.StateAttribute";
    private const string EventAttr = "FrenchExDev.Net.FiniteStateMachine.Attributes.EventAttribute";
    private const string RichTransAttr = "FrenchExDev.Net.FiniteStateMachine.Attributes.RichTransitionAttribute";
    private const string RichSmEventsAttr = "FrenchExDev.Net.FiniteStateMachine.Attributes.RichStateMachineEventsAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                RichSmAttr,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, ct) => GetModel(ctx, ct))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(models, static (ctx, model) =>
        {
            // Emit visitor + Match extensions
            var visitorSource = RichEmitter.EmitVisitorAndMatch(model!);
            ctx.AddSource($"{model!.DomainName}Visitors.g.cs", SourceText.From(visitorSource, Encoding.UTF8));

            // Emit Accept on each state record
            foreach (var state in model.States)
            {
                var acceptSource = RichEmitter.EmitAcceptForState(model, state);
                ctx.AddSource($"{state.TypeName}.Accept.g.cs", SourceText.From(acceptSource, Encoding.UTF8));
            }

            // Emit Accept on each event record
            foreach (var evt in model.Events)
            {
                var acceptSource = RichEmitter.EmitAcceptForEvent(model, evt);
                ctx.AddSource($"{evt.TypeName}.Accept.g.cs", SourceText.From(acceptSource, Encoding.UTF8));
            }
        });
    }

    private static RichEmitModel? GetModel(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol stateInterface)
            return null;

        ct.ThrowIfCancellationRequested();

        var attr = ctx.Attributes[0];
        var initialStateTypeName = ReadRichStateMachineAttribute(attr);

        var ns = stateInterface.ContainingNamespace is { IsGlobalNamespace: false } nsSym
            ? nsSym.ToDisplayString()
            : string.Empty;

        var allTypes = GetAllTypes(ctx.SemanticModel.Compilation.GlobalNamespace).ToList();
        var (states, events, eventInterface) = CollectStatesAndEvents(allTypes, stateInterface, ct);
        var transitions = CollectRichTransitions(allTypes, ct);

        if (states.Count == 0)
            return null;

        return new RichEmitModel(
            ns,
            stateInterface.Name,
            eventInterface?.Name ?? "IEvent",
            initialStateTypeName,
            states,
            events,
            transitions);
    }

    private static string? ReadRichStateMachineAttribute(AttributeData attr)
    {
        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key == "InitialState" && arg.Value.Value is INamedTypeSymbol initType)
                return initType.Name;
        }

        return null;
    }

    private static (List<RichStateEmitModel> states, List<RichEventEmitModel> events, INamedTypeSymbol? eventInterface) CollectStatesAndEvents(
        IEnumerable<INamedTypeSymbol> allTypes,
        INamedTypeSymbol stateInterface,
        CancellationToken ct)
    {
        var states = new List<RichStateEmitModel>();
        var events = new List<RichEventEmitModel>();
        INamedTypeSymbol? eventInterface = null;

        foreach (var type in allTypes)
        {
            ct.ThrowIfCancellationRequested();
            ProcessTypeForStatesAndEvents(type, stateInterface, states, events, ref eventInterface);
        }

        return (states, events, eventInterface);
    }

    private static void ProcessTypeForStatesAndEvents(
        INamedTypeSymbol type,
        INamedTypeSymbol stateInterface,
        List<RichStateEmitModel> states,
        List<RichEventEmitModel> events,
        ref INamedTypeSymbol? eventInterface)
    {
        foreach (var typeAttr in type.GetAttributes())
        {
            var attrName = typeAttr.AttributeClass?.ToDisplayString();

            if (attrName == StateAttr)
            {
                var state = TryExtractState(type, typeAttr, stateInterface);
                if (state != null)
                    states.Add(state);
            }
            else if (attrName == EventAttr)
            {
                eventInterface ??= FindEventInterface(type);
                events.Add(TryExtractEvent(type));
            }
        }
    }

    private static RichStateEmitModel? TryExtractState(INamedTypeSymbol type, AttributeData attr, INamedTypeSymbol stateInterface)
    {
        if (!type.AllInterfaces.Contains(stateInterface, SymbolEqualityComparer.Default) &&
            !type.Interfaces.Contains(stateInterface, SymbolEqualityComparer.Default))
            return null;

        var isTerminal = false;
        foreach (var na in attr.NamedArguments)
        {
            if (na.Key == "Terminal" && na.Value.Value is bool b)
                isTerminal = b;
        }

        return new RichStateEmitModel(type.Name, type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), isTerminal);
    }

    private static RichEventEmitModel TryExtractEvent(INamedTypeSymbol type)
    {
        return new RichEventEmitModel(type.Name, type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    private static INamedTypeSymbol? FindEventInterface(INamedTypeSymbol eventType)
    {
        foreach (var iface in eventType.Interfaces.Concat(eventType.AllInterfaces))
        {
            foreach (var ifaceAttr in iface.GetAttributes())
            {
                if (ifaceAttr.AttributeClass?.ToDisplayString() == RichSmEventsAttr)
                    return iface;
            }
        }

        return null;
    }

    private static List<RichTransitionEmitModel> CollectRichTransitions(
        IEnumerable<INamedTypeSymbol> allTypes,
        CancellationToken ct)
    {
        var transitions = new List<RichTransitionEmitModel>();

        foreach (var type in allTypes)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var typeAttr in type.GetAttributes())
            {
                if (typeAttr.AttributeClass?.ToDisplayString() != RichTransAttr)
                    continue;

                if (typeAttr.ConstructorArguments.Length < 2)
                    continue;

                var fromType = typeAttr.ConstructorArguments[0].Value as INamedTypeSymbol;
                var toType = typeAttr.ConstructorArguments[1].Value as INamedTypeSymbol;
                if (fromType != null && toType != null)
                {
                    transitions.Add(new RichTransitionEmitModel(
                        fromType.Name, toType.Name, type.Name));
                }
            }
        }

        return transitions;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamedTypeSymbol type)
                yield return type;
            else if (member is INamespaceSymbol childNs)
            {
                foreach (var t in GetAllTypes(childNs))
                    yield return t;
            }
        }
    }
}
