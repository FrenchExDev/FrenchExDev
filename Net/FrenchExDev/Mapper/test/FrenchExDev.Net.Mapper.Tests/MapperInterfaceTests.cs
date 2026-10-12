using FrenchExDev.Net.Mapper;
using FrenchExDev.Net.Mapper.Testing;
using Xunit;

namespace FrenchExDev.Net.Mapper.Tests;

public sealed class Source
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public sealed class Target
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }

    public override bool Equals(object? obj) =>
        obj is Target other && Name == other.Name && Age == other.Age;

    public override int GetHashCode() => HashCode.Combine(Name, Age);
}

public sealed class ManualMapper : IMapper<Source, Target>
{
    public Target Map(Source source) => new()
    {
        Name = source.Name,
        Age = source.Age,
    };
}

public sealed class MapperInterfaceTests
{
    [Fact]
    public void IMapper_CanBeImplementedManually()
    {
        IMapper<Source, Target> mapper = new ManualMapper();
        Assert.NotNull(mapper);
    }

    [Fact]
    public void ManualMapper_MapsCorrectly()
    {
        var mapper = new ManualMapper();
        var source = new Source { Name = "Alice", Age = 30 };

        var target = mapper.Map(source);

        Assert.Equal("Alice", target.Name);
        Assert.Equal(30, target.Age);
    }

    [Fact]
    public void ShouldMapTo_Succeeds_WhenMappedResultEqualsExpected()
    {
        var mapper = new ManualMapper();
        var source = new Source { Name = "Bob", Age = 25 };
        var expected = new Target { Name = "Bob", Age = 25 };

        mapper.ShouldMapTo(source, expected);
    }

    [Fact]
    public void ShouldMapTo_Throws_WhenMappedResultDiffers()
    {
        var mapper = new ManualMapper();
        var source = new Source { Name = "Bob", Age = 25 };
        var wrong = new Target { Name = "Wrong", Age = 99 };

        Assert.Throws<MapperAssertionException>(() => mapper.ShouldMapTo(source, wrong));
    }

    [Fact]
    public void ShouldMapProperty_Succeeds_WhenPropertyMatches()
    {
        var mapper = new ManualMapper();
        var source = new Source { Name = "Carol", Age = 40 };

        mapper.ShouldMapProperty(source, t => t.Name, "Carol");
    }

    [Fact]
    public void ShouldMapProperty_Throws_WhenPropertyDiffers()
    {
        var mapper = new ManualMapper();
        var source = new Source { Name = "Carol", Age = 40 };

        Assert.Throws<MapperAssertionException>(() =>
            mapper.ShouldMapProperty(source, t => t.Name, "Wrong"));
    }

    [Fact]
    public void ShouldMapProperty_ChecksAge()
    {
        var mapper = new ManualMapper();
        var source = new Source { Name = "Dave", Age = 55 };

        mapper.ShouldMapProperty(source, t => t.Age, 55);
    }
}
