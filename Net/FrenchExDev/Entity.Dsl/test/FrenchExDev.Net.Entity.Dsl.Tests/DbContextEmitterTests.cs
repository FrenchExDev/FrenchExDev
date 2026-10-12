using FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;
using Xunit;

namespace FrenchExDev.Net.Entity.Dsl.Tests;

public class DbContextEmitterTests
{
    private static DbContextEmitModel CreateModel() => new()
    {
        Namespace = "MyApp.Infrastructure",
        ClassName = "SalesDbContext",
        DbSets =
        [
            new DbSetModel { EntityTypeFull = "global::MyApp.Domain.Order", PropertyName = "Orders" },
            new DbSetModel { EntityTypeFull = "global::MyApp.Domain.Customer", PropertyName = "Customers" }
        ]
    };

    [Fact]
    public void EmitBase_contains_dbset_properties()
    {
        var code = DbContextEmitter.EmitBase(CreateModel());
        Assert.Contains("DbSet<global::MyApp.Domain.Order> Orders", code);
        Assert.Contains("DbSet<global::MyApp.Domain.Customer> Customers", code);
    }

    [Fact]
    public void EmitBase_contains_register_configurations()
    {
        var code = DbContextEmitter.EmitBase(CreateModel());
        Assert.Contains("RegisterConfigurations", code);
        Assert.Contains("ApplyConfiguration", code);
    }

    [Fact]
    public void EmitBase_contains_pre_and_post_model_creating_hooks()
    {
        var code = DbContextEmitter.EmitBase(CreateModel());
        Assert.Contains("PreModelCreating", code);
        Assert.Contains("PostModelCreating", code);
    }

    [Fact]
    public void EmitBase_contains_lifecycle_hooks()
    {
        var code = DbContextEmitter.EmitBase(CreateModel());
        Assert.Contains("OnEntitiesAdding", code);
        Assert.Contains("OnEntitiesModifying", code);
        Assert.Contains("OnEntitiesDeleting", code);
    }

    [Fact]
    public void EmitBase_save_changes_calls_on_before_save()
    {
        var code = DbContextEmitter.EmitBase(CreateModel());
        Assert.Contains("OnBeforeSaveChanges()", code);
        Assert.Contains("override int SaveChanges", code);
        Assert.Contains("override global::System.Threading.Tasks.Task<int> SaveChangesAsync", code);
    }

    [Fact]
    public void EmitBase_class_is_abstract()
    {
        var code = DbContextEmitter.EmitBase(CreateModel());
        Assert.Contains("public abstract class SalesDbContextBase : Microsoft.EntityFrameworkCore.DbContext", code);
    }

    [Fact]
    public void EmitBase_on_model_creating_is_sealed()
    {
        var code = DbContextEmitter.EmitBase(CreateModel());
        Assert.Contains("protected sealed override void OnModelCreating", code);
    }

    [Fact]
    public void EmitBase_excludes_keyless_from_dbsets()
    {
        var model = CreateModel();
        model.DbSets.Add(new DbSetModel
        {
            EntityTypeFull = "global::MyApp.Domain.ActiveOrderView",
            PropertyName = "ActiveOrderViews",
            IsKeyless = true
        });
        var code = DbContextEmitter.EmitBase(model);
        Assert.DoesNotContain("ActiveOrderViews", code);
    }

    [Fact]
    public void EmitPartialStub_extends_base()
    {
        var code = DbContextEmitter.EmitPartialStub(CreateModel());
        Assert.Contains("public partial class SalesDbContext : SalesDbContextBase", code);
    }

    [Fact]
    public void EmitPartialStub_has_constructor()
    {
        var code = DbContextEmitter.EmitPartialStub(CreateModel());
        Assert.Contains("public SalesDbContext(", code);
        Assert.Contains("DbContextOptions<SalesDbContext>", code);
    }
}
