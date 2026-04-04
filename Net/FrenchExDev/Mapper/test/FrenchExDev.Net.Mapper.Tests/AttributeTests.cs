using System.Reflection;
using FrenchExDev.Net.Mapper.Attributes;
using Xunit;

namespace FrenchExDev.Net.Mapper.Tests;

public sealed class TargetDto;

public sealed class SourceDto;

[MapTo(typeof(TargetDto))]
[OneWay]
public sealed class AnnotatedSource
{
    [MapProperty("OriginalName")]
    public string RenamedProp { get; set; } = string.Empty;

    [IgnoreMapping]
    public int Ignored { get; set; }
}

[MapFrom(typeof(SourceDto))]
public sealed class AnnotatedTarget;

public sealed class AttributeTests
{
    [Fact]
    public void MapToAttribute_StoresTargetType()
    {
        var attr = new MapToAttribute(typeof(TargetDto));
        Assert.Equal(typeof(TargetDto), attr.TargetType);
    }

    [Fact]
    public void MapFromAttribute_StoresSourceType()
    {
        var attr = new MapFromAttribute(typeof(SourceDto));
        Assert.Equal(typeof(SourceDto), attr.SourceType);
    }

    [Fact]
    public void MapPropertyAttribute_StoresSourcePropertyName()
    {
        var attr = new MapPropertyAttribute("OriginalName");
        Assert.Equal("OriginalName", attr.SourcePropertyName);
    }

    [Fact]
    public void IgnoreMappingAttribute_CanBeApplied()
    {
        var prop = typeof(AnnotatedSource).GetProperty(nameof(AnnotatedSource.Ignored));
        var attr = prop!.GetCustomAttribute<IgnoreMappingAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void OneWayAttribute_CanBeApplied()
    {
        var attr = typeof(AnnotatedSource).GetCustomAttribute<OneWayAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void MapToAttribute_CanBeRetrievedFromClass()
    {
        var attr = typeof(AnnotatedSource).GetCustomAttribute<MapToAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(typeof(TargetDto), attr.TargetType);
    }

    [Fact]
    public void MapFromAttribute_CanBeRetrievedFromClass()
    {
        var attr = typeof(AnnotatedTarget).GetCustomAttribute<MapFromAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(typeof(SourceDto), attr.SourceType);
    }

    [Fact]
    public void MapPropertyAttribute_CanBeRetrievedFromProperty()
    {
        var prop = typeof(AnnotatedSource).GetProperty(nameof(AnnotatedSource.RenamedProp));
        var attr = prop!.GetCustomAttribute<MapPropertyAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("OriginalName", attr.SourcePropertyName);
    }
}
