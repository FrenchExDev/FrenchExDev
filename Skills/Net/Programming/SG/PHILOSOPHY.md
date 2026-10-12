# SG (Source Generators) — Philosophy

## Source Generation Over Runtime Reflection

Always prefer compile-time source generation over runtime reflection. Source generators provide:
- **Compile-time safety** — errors at build, not at runtime
- **Zero runtime cost** — no reflection overhead, no `System.Reflection` dependency
- **AOT compatibility** — generated code is plain C#, fully trimmable
- **IDE support** — generated types are visible in IntelliSense

## The .Lib Separation Principle

Every source generator must separate **Roslyn extraction** from **code emission**:

- `.SourceGenerator` (netstandard2.0) — depends on Roslyn. Extracts `ISymbol`/`SyntaxNode` data into plain model objects.
- `.SourceGenerator.Lib` (netstandard2.0, **NO Roslyn dependency**) — receives plain models, emits C# strings.

Why this matters:
- **Reusability** — `BuilderEmitter` in `.Lib` is shared by Builder, BinaryWrapper, DockerCompose, and Diem generators. Four generators, one emitter.
- **Testability** — `.Lib` can be unit-tested without Roslyn infrastructure.
- **Dependency isolation** — `.Lib` has zero Roslyn references, making it safe to consume from any project.

See: `Builder/src/FrenchExDev.Net.Builder.SourceGenerator.Lib/`

## 2-Step Pattern: Extract Then Emit

Every generator follows the same 2-step pattern:

1. **Extract** (in `.SourceGenerator`) — parse Roslyn symbols into plain data models (strings, bools, lists). No Roslyn types leak out.
2. **Emit** (in `.SourceGenerator.Lib`) — stateless emitter function takes the model and returns a C# string.

```csharp
// Step 1: Extract (Roslyn-dependent)
var model = GetModel(ctx, ct);  // IPropertySymbol -> BuilderEmitModel

// Step 2: Emit (Roslyn-free)
var source = BuilderEmitter.Emit(model);  // BuilderEmitModel -> string
```

## BuilderEmitter as Shared Infrastructure

`BuilderEmitter.Emit()` is the single builder code emitter for the entire codebase. Never write a second builder emitter. When domain-specific behavior is needed, use extension points:

- `BuilderEmitModel.Preamble` — raw C# injected after class opening brace
- `BuilderPropertyModel.WithMethodAttributes` — attributes on generated `With*()` methods
- `BuilderPropertyModel.WithMethodBodyPrefix` — code before the assignment in `With*()`
- `BuilderPropertyModel.InstantiationExpression` — custom expression in `CreateInstance()`

See: `Builder/src/FrenchExDev.Net.Builder.SourceGenerator.Lib/BuilderEmitModel.cs`

## Incremental Only

Always use `IIncrementalGenerator`, never legacy `ISourceGenerator`. Always use `ForAttributeWithMetadataName` for attribute-triggered generators — it is significantly faster than `ForSyntaxProvider` because Roslyn caches attribute lookup.

```csharp
[Generator]
public sealed class MyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "My.Namespace.MyAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => GetModel(ctx, ct))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(models, static (ctx, model) => { /* emit */ });
    }
}
```

## Extension Points Over Forks

When a domain SG needs custom behavior in generated builders, use `BuilderEmitModel` extension points. Never fork `BuilderEmitter`. This is the Open/Closed Principle applied to code generation.

## String Building Over SyntaxFactory

Emit code via `StringBuilder` and string interpolation, not Roslyn `SyntaxFactory`. Why:
- Readable — you can see the generated output in the emitter code
- Debuggable — `Console.WriteLine(source)` shows exactly what will be emitted
- No Roslyn dependency in `.Lib` — `SyntaxFactory` requires `Microsoft.CodeAnalysis`
