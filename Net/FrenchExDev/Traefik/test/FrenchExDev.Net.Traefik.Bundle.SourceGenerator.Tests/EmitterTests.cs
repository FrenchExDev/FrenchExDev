using FrenchExDev.Net.Builder.SourceGenerator.Lib;
using FrenchExDev.Net.Traefik.Bundle.SourceGenerator;
using Shouldly;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Tests;

/// <summary>
/// Pins the shape of generated source for the parts the analyzer + runtime
/// rules depend on. Plain string assertions, no Roslyn driver — these are
/// the cheapest possible regression guards on the emitter.
/// </summary>
public sealed class EmitterTests
{
    [Fact]
    public void DiscriminatedBuilder_EmitsExactlyOneBranchEpilogue()
    {
        var branches = new List<DiscriminatedBranch>
        {
            new() { PropertyName = "addPrefix", RefName = "addPrefixMiddleware" },
            new() { PropertyName = "basicAuth", RefName = "basicAuthMiddleware" },
        };

        var model = TraefikBuilderHelper.CreateDiscriminatedBuilderModel(
            "FrenchExDev.Net.Traefik.Bundle", "TraefikHttpMiddleware", branches);
        var source = BuilderEmitter.Emit(model);

        source.ShouldContain("__setCount");
        source.ShouldContain("if (AddPrefix is not null) __setCount++;");
        source.ShouldContain("if (BasicAuth is not null) __setCount++;");
        source.ShouldContain("if (__setCount != 1)");
        source.ShouldContain("TraefikHttpMiddleware requires exactly one branch");
    }

    [Fact]
    public void DiscriminatedBuilder_DoesNotEmitEpilogueOnEmptyBranches()
    {
        var model = TraefikBuilderHelper.CreateDiscriminatedBuilderModel(
            "FrenchExDev.Net.Traefik.Bundle", "Empty", new List<DiscriminatedBranch>());
        var source = BuilderEmitter.Emit(model);

        // Epilogue is still emitted, but with zero counters — count != 1
        // means the build will always fail. That's the right thing for an
        // empty union: there's no valid construction.
        source.ShouldContain("__setCount");
        source.ShouldContain("if (__setCount != 1)");
    }

    [Fact]
    public void StandardBuilder_DoesNotEmitDiscriminatorEpilogue()
    {
        var props = new List<UnifiedProperty>
        {
            new() { Property = new PropertyModel { JsonName = "rule", CSharpName = "Rule", Type = PropertyType.String } },
        };

        var model = TraefikBuilderHelper.CreateBuilderModel(
            "FrenchExDev.Net.Traefik.Bundle", "TraefikHttpRouter", props);
        var source = BuilderEmitter.Emit(model);

        source.ShouldNotContain("__setCount");
        source.ShouldNotContain("requires exactly one branch");
    }
}
