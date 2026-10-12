using System.Reflection;
using FrenchExDev.Net.Diem.Admin.Actions.Attributes;
using FrenchExDev.Net.Diem.Admin.Forms.Attributes;
using FrenchExDev.Net.Diem.Admin.Lists.Attributes;
using FrenchExDev.Net.Dsl;
using Xunit;

namespace FrenchExDev.Net.Diem.Admin.Tests;

public class AdminAttributeTests
{
    [Fact]
    public void AdminModuleAttribute_HasMetaConcept()
    {
        var attr = typeof(AdminModuleAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void AdminFilterAttribute_HasMetaConcept()
    {
        var attr = typeof(AdminFilterAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void AdminFieldAttribute_HasMetaConcept()
    {
        var attr = typeof(AdminFieldAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void AdminActionAttribute_HasMetaConcept()
    {
        var attr = typeof(AdminActionAttribute).GetCustomAttribute<MetaConceptAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void AdminModuleAttribute_HasNameAndAggregate()
    {
        var attr = new AdminModuleAttribute("Products", typeof(string));
        Assert.Equal("Products", attr.Name);
        Assert.Equal(typeof(string), attr.Aggregate);
    }

    [Fact]
    public void AdminFilterAttribute_DefaultsFilterTypeToText()
    {
        var attr = new AdminFilterAttribute("Status");
        Assert.Equal("Text", attr.FilterType);
    }

    [Fact]
    public void AdminFieldAttribute_HasOrderProperty()
    {
        var attr = new AdminFieldAttribute("Title") { Order = 5 };
        Assert.Equal(5, attr.Order);
    }

    [Fact]
    public void AdminActionAttribute_HasCommandProperty()
    {
        var attr = new AdminActionAttribute("Publish") { Command = "PublishCommand" };
        Assert.Equal("PublishCommand", attr.Command);
    }
}
