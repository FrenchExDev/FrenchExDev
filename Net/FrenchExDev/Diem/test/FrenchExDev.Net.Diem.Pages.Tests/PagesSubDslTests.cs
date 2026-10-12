using System.Reflection;
using Xunit;
using FrenchExDev.Net.Dsl;
using FrenchExDev.Net.Diem.Pages.Widgets.Attributes;
using FrenchExDev.Net.Diem.Pages.Layouts.Attributes;
using FrenchExDev.Net.Diem.Pages.Routing.Attributes;
using FrenchExDev.Net.Diem.Pages.Layouts;
using FrenchExDev.Net.Diem.Pages.Routing;

namespace FrenchExDev.Net.Diem.Pages.Tests;

public class WidgetAttributeTests
{
    [Fact]
    public void PageWidgetAttribute_Has_MetaConcept()
    {
        var attr = typeof(PageWidgetAttribute)
            .GetCustomAttribute<MetaConceptAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("PageWidget", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    [Fact]
    public void WidgetConfigAttribute_Has_MetaConcept()
    {
        var attr = typeof(WidgetConfigAttribute)
            .GetCustomAttribute<MetaConceptAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("WidgetConfig", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    [Fact]
    public void PageWidgetAttribute_Stores_Name()
    {
        var pw = new PageWidgetAttribute("Hero");
        Assert.Equal("Hero", pw.Name);
    }

    [Fact]
    public void WidgetConfigAttribute_Required_DefaultsFalse()
    {
        var wc = new WidgetConfigAttribute();
        Assert.False(wc.Required);
    }
}

public class LayoutAttributeTests
{
    [Fact]
    public void LayoutAttribute_Has_MetaConcept()
    {
        var attr = typeof(LayoutAttribute)
            .GetCustomAttribute<MetaConceptAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("Layout", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    [Fact]
    public void AreaAttribute_Has_MetaConcept()
    {
        var attr = typeof(AreaAttribute)
            .GetCustomAttribute<MetaConceptAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("Area", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }

    [Fact]
    public void ZoneAttribute_Has_MetaConcept()
    {
        var attr = typeof(ZoneAttribute)
            .GetCustomAttribute<MetaConceptAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("Zone", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }
}

public class RoutingAttributeTests
{
    [Fact]
    public void BoundEntityAttribute_Has_MetaConcept()
    {
        var attr = typeof(BoundEntityAttribute)
            .GetCustomAttribute<MetaConceptAttribute>();

        Assert.NotNull(attr);
        Assert.Equal("BoundEntity", ((MetaConcept)Activator.CreateInstance(attr.ConceptType)!).Name);
    }
}

public class PageEntityTests
{
    [Fact]
    public void Page_Has_Required_Properties()
    {
        var page = new Page();

        // All properties should exist and have defaults
        Assert.Equal(Guid.Empty, page.Id);
        Assert.Equal("", page.Title);
        Assert.Equal("", page.Slug);
        Assert.Equal("", page.MaterializedPath);
        Assert.Null(page.ParentId);
        Assert.Equal(Guid.Empty, page.LayoutId);
    }

    [Fact]
    public void Layout_Has_Areas_Collection()
    {
        var layout = new Layout();
        Assert.NotNull(layout.Areas);
        Assert.Empty(layout.Areas);
    }

    [Fact]
    public void Area_Has_Zones_Collection()
    {
        var area = new Area();
        Assert.NotNull(area.Zones);
        Assert.Empty(area.Zones);
    }

    [Fact]
    public void Zone_MaxWidgets_Defaults_To_10()
    {
        var zone = new Zone();
        Assert.Equal(10, zone.MaxWidgets);
    }
}

public class PageRouterTests
{
    private readonly PageRouter _router = new();

    private static List<Page> CreateTestPages() =>
    [
        new Page { MaterializedPath = "/", IsPublished = true, Title = "Home" },
        new Page { MaterializedPath = "/about", IsPublished = true, Title = "About" },
        new Page { MaterializedPath = "/products", IsPublished = true, Title = "Products" },
        new Page { MaterializedPath = "/draft", IsPublished = false, Title = "Draft" },
    ];

    [Fact]
    public void Resolve_ExactPath_ReturnsPage()
    {
        var pages = CreateTestPages();
        var result = _router.Resolve("/about", pages);

        Assert.NotNull(result);
        Assert.Equal("About", result.Page.Title);
        Assert.Null(result.EntitySlug);
    }

    [Fact]
    public void Resolve_ParentPath_WithEntitySlug()
    {
        var pages = CreateTestPages();
        var result = _router.Resolve("/products/running-shoes", pages);

        Assert.NotNull(result);
        Assert.Equal("Products", result.Page.Title);
        Assert.Equal("running-shoes", result.EntitySlug);
    }

    [Fact]
    public void Resolve_UnmatchedPath_ReturnsNull()
    {
        var pages = CreateTestPages();
        var result = _router.Resolve("/nonexistent", pages);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_UnpublishedPage_ReturnsNull()
    {
        var pages = CreateTestPages();
        var result = _router.Resolve("/draft", pages);

        Assert.Null(result);
    }
}
