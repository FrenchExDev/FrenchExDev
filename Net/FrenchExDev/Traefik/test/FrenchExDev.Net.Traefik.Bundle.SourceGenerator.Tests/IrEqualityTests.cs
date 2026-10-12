using FrenchExDev.Net.Traefik.Bundle.SourceGenerator;
using Shouldly;

namespace FrenchExDev.Net.Traefik.Bundle.SourceGenerator.Tests;

/// <summary>
/// The whole point of structural equality on the IR types is so the
/// incremental generator can cache the emit stage. These tests pin the
/// behaviour for the cases the pipeline relies on.
/// </summary>
public sealed class IrEqualityTests
{
    [Fact]
    public void PropertyModel_StructurallyEqual_WhenAllFieldsMatch()
    {
        var a = Build();
        var b = Build();

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());

        static PropertyModel Build() => new()
        {
            JsonName = "entryPoints",
            CSharpName = "EntryPoints",
            Description = "EntryPoints holds the EntryPoints configuration.",
            Type = PropertyType.DictOfRef,
            DictOfRefTarget = "entryPoint",
        };
    }

    [Fact]
    public void PropertyModel_NotEqual_WhenDescriptionDiffers()
    {
        var a = new PropertyModel { JsonName = "x", CSharpName = "X", Description = "old" };
        var b = new PropertyModel { JsonName = "x", CSharpName = "X", Description = "new" };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void PropertyModel_StructurallyEqual_OnNestedItems()
    {
        PropertyModel Build() => new()
        {
            JsonName = "list",
            CSharpName = "List",
            Type = PropertyType.Array,
            Items = new PropertyModel
            {
                JsonName = "item",
                CSharpName = "Item",
                Type = PropertyType.Ref,
                Ref = "router",
            },
        };

        Build().ShouldBe(Build());
    }

    [Fact]
    public void DefinitionModel_StructurallyEqual_OnBranchOrder()
    {
        var a = new DefinitionModel
        {
            Name = "httpMiddleware",
            IsOneOfDiscriminated = true,
            Branches = new()
            {
                new DiscriminatedBranch { PropertyName = "addPrefix", RefName = "addPrefixMiddleware" },
                new DiscriminatedBranch { PropertyName = "basicAuth", RefName = "basicAuthMiddleware" },
            },
        };
        var b = new DefinitionModel
        {
            Name = "httpMiddleware",
            IsOneOfDiscriminated = true,
            Branches = new()
            {
                new DiscriminatedBranch { PropertyName = "addPrefix", RefName = "addPrefixMiddleware" },
                new DiscriminatedBranch { PropertyName = "basicAuth", RefName = "basicAuthMiddleware" },
            },
        };

        a.ShouldBe(b);
    }

    [Fact]
    public void SchemaModel_StructurallyEqual_DespiteSeparateInstances()
    {
        SchemaModel Build()
        {
            var m = new SchemaModel { Version = "3", Kind = SchemaKind.Static };
            m.RootProperties.Add(new PropertyModel { JsonName = "log", CSharpName = "Log", Type = PropertyType.Ref, Ref = "log" });
            m.Definitions["log"] = new DefinitionModel
            {
                Name = "log",
                Properties =
                {
                    new PropertyModel { JsonName = "level", CSharpName = "Level", Type = PropertyType.String },
                },
            };
            return m;
        }

        Build().ShouldBe(Build());
    }
}
