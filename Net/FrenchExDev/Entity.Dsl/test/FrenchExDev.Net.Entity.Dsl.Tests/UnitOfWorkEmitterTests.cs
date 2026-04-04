using FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;
using Xunit;

namespace FrenchExDev.Net.Entity.Dsl.Tests;

public class UnitOfWorkEmitterTests
{
    private static UnitOfWorkEmitModel CreateModel() => new()
    {
        Namespace = "MyApp.Infrastructure",
        DbContextClassName = "SalesDbContext",
        DbContextClassFull = "global::MyApp.Infrastructure.SalesDbContext",
        Repositories =
        [
            new UnitOfWorkRepositoryModel
            {
                InterfaceTypeFull = "global::MyApp.Domain.Repositories.IOrderRepository",
                ImplementationTypeFull = "global::MyApp.Domain.Repositories.OrderRepository",
                PropertyName = "Orders"
            },
            new UnitOfWorkRepositoryModel
            {
                InterfaceTypeFull = "global::MyApp.Domain.Repositories.ICustomerRepository",
                ImplementationTypeFull = "global::MyApp.Domain.Repositories.CustomerRepository",
                PropertyName = "Customers"
            }
        ]
    };

    [Fact]
    public void EmitInterface_extends_iunitofwork()
    {
        var code = UnitOfWorkEmitter.EmitInterface(CreateModel());
        Assert.Contains("ISalesDbContextUnitOfWork : global::FrenchExDev.Net.Entity.Dsl.Abstractions.IUnitOfWork<global::MyApp.Infrastructure.SalesDbContext>", code);
    }

    [Fact]
    public void EmitInterface_has_repository_properties()
    {
        var code = UnitOfWorkEmitter.EmitInterface(CreateModel());
        Assert.Contains("IOrderRepository Orders", code);
        Assert.Contains("ICustomerRepository Customers", code);
    }

    [Fact]
    public void EmitBase_has_lazy_repository_properties()
    {
        var code = UnitOfWorkEmitter.EmitBase(CreateModel());
        Assert.Contains("_orders ??=", code);
        Assert.Contains("_customers ??=", code);
    }

    [Fact]
    public void EmitBase_has_virtual_factory_methods()
    {
        var code = UnitOfWorkEmitter.EmitBase(CreateModel());
        Assert.Contains("protected virtual", code);
        Assert.Contains("CreateOrdersRepository()", code);
        Assert.Contains("CreateCustomersRepository()", code);
    }

    [Fact]
    public void EmitBase_has_save_changes()
    {
        var code = UnitOfWorkEmitter.EmitBase(CreateModel());
        Assert.Contains("SaveChangesAsync", code);
        Assert.Contains("SaveChanges()", code);
    }

    [Fact]
    public void EmitBase_has_transaction_support()
    {
        var code = UnitOfWorkEmitter.EmitBase(CreateModel());
        Assert.Contains("BeginTransactionAsync", code);
        Assert.Contains("CommitAsync", code);
        Assert.Contains("RollbackAsync", code);
    }

    [Fact]
    public void EmitPartialStub_has_injectable_attribute()
    {
        var code = UnitOfWorkEmitter.EmitPartialStub(CreateModel());
        Assert.Contains("[global::FrenchExDev.Net.Injectable.Attributes.Injectable(", code);
        Assert.Contains("As = typeof(ISalesDbContextUnitOfWork)", code);
    }

    [Fact]
    public void EmitPartialStub_extends_base()
    {
        var code = UnitOfWorkEmitter.EmitPartialStub(CreateModel());
        Assert.Contains("public partial class SalesDbContextUnitOfWork : SalesDbContextUnitOfWorkBase", code);
    }
}
