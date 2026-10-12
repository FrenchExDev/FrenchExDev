using FrenchExDev.Net.Entity.Dsl.SourceGenerator.Lib;
using Xunit;

namespace FrenchExDev.Net.Entity.Dsl.Tests;

public class DbContextRegistrationEmitterTests
{
    [Fact]
    public void Emit_generates_add_dbcontext_extension()
    {
        var model = new DbContextEmitModel
        {
            Namespace = "MyApp.Infrastructure",
            ClassName = "SalesDbContext"
        };
        var code = DbContextRegistrationEmitter.Emit(model);
        Assert.Contains("AddSalesDbContext", code);
        Assert.Contains("AddDbContext<MyApp.Infrastructure.SalesDbContext>", code);
    }

    [Fact]
    public void Emit_returns_iservicecollection()
    {
        var model = new DbContextEmitModel
        {
            Namespace = "MyApp.Infrastructure",
            ClassName = "SalesDbContext"
        };
        var code = DbContextRegistrationEmitter.Emit(model);
        Assert.Contains("return services;", code);
    }

    [Fact]
    public void Emit_namespace_is_di()
    {
        var model = new DbContextEmitModel
        {
            Namespace = "MyApp.Infrastructure",
            ClassName = "SalesDbContext"
        };
        var code = DbContextRegistrationEmitter.Emit(model);
        Assert.Contains("namespace Microsoft.Extensions.DependencyInjection;", code);
    }
}
