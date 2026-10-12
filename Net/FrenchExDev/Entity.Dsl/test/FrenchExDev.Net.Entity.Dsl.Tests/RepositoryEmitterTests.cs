using FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;
using Xunit;

namespace FrenchExDev.Net.Entity.Dsl.Tests;

public class RepositoryEmitterTests
{
    private static RepositoryEmitModel CreateModel() => new()
    {
        Namespace = "MyApp.Domain",
        EntityClassName = "Order",
        EntityClassFull = "global::MyApp.Domain.Order",
        PrimaryKeyTypeFull = "global::System.Guid",
        DbContextTypeFull = "global::MyApp.Infrastructure.SalesDbContext"
    };

    [Fact]
    public void EmitInterface_extends_irepository()
    {
        var code = RepositoryEmitter.EmitInterface(CreateModel());
        Assert.Contains("IOrderRepository : global::FrenchExDev.Net.Entity.Dsl.Abstractions.IRepository<global::MyApp.Domain.Order>", code);
    }

    [Fact]
    public void EmitInterface_has_typed_find_by_id()
    {
        var code = RepositoryEmitter.EmitInterface(CreateModel());
        Assert.Contains("FindByIdAsync(global::System.Guid id)", code);
    }

    [Fact]
    public void EmitPartialStub_has_injectable_attribute()
    {
        var code = RepositoryEmitter.EmitPartialStub(CreateModel());
        Assert.Contains("[global::FrenchExDev.Net.Injectable.Attributes.Injectable(", code);
        Assert.Contains("Scope = global::FrenchExDev.Net.Injectable.Attributes.Scope.Scoped", code);
        Assert.Contains("I{0}Repository".Replace("{0}", "Order"), code);
    }

    [Fact]
    public void EmitPartialStub_defaults_to_abstractions_repository_base()
    {
        var code = RepositoryEmitter.EmitPartialStub(CreateModel());
        Assert.Contains("OrderRepository : global::FrenchExDev.Net.Entity.Dsl.Abstractions.RepositoryBase<global::MyApp.Domain.Order>", code);
        Assert.Contains("IOrderRepository", code);
    }

    [Fact]
    public void EmitPartialStub_uses_custom_repository_base_when_set()
    {
        var model = CreateModel();
        model.RepositoryBaseTypeFull = "global::MyApp.MyProjectRepository<global::MyApp.Domain.Order>";
        var code = RepositoryEmitter.EmitPartialStub(model);
        Assert.Contains("OrderRepository : global::MyApp.MyProjectRepository<global::MyApp.Domain.Order>, IOrderRepository", code);
        Assert.DoesNotContain("RepositoryBase<", code);
    }

    [Fact]
    public void EmitPartialStub_has_typed_find_by_id_bridge()
    {
        var code = RepositoryEmitter.EmitPartialStub(CreateModel());
        Assert.Contains("FindByIdAsync(global::System.Guid id)", code);
        Assert.Contains("FindByIdAsync(new object[] { id })", code);
    }

    [Fact]
    public void EmitPartialStub_has_constructor_with_typed_dbcontext()
    {
        var code = RepositoryEmitter.EmitPartialStub(CreateModel());
        Assert.Contains("OrderRepository(global::MyApp.Infrastructure.SalesDbContext context)", code);
    }

    [Fact]
    public void EmitPartialStub_implements_interface()
    {
        var code = RepositoryEmitter.EmitPartialStub(CreateModel());
        Assert.Contains("IOrderRepository", code);
    }
}
