using FrenchExDev.Net.Injectable.SourceGenerator.Lib;
using Xunit;

namespace FrenchExDev.Net.Injectable.SourceGenerator.Lib.Tests;

public class SanitizeNameTests
{
    [Theory]
    [InlineData("MyApp", "MyApp")]
    [InlineData("MyApp.Services", "MyAppServices")]
    [InlineData("My.App.Core", "MyAppCore")]
    [InlineData("acme-web-api", "AcmeWebApi")]
    [InlineData("some_lib_name", "SomeLibName")]
    [InlineData("mixed.dash-under_space dot", "MixedDashUnderSpaceDot")]
    [InlineData("already", "Already")]
    [InlineData("A", "A")]
    [InlineData("a", "A")]
    [InlineData("a.b.c", "ABC")]
    public void Strips_separators_and_pascal_cases(string input, string expected)
    {
        Assert.Equal(expected, InjectableEmitter.SanitizeName(input));
    }

    [Fact]
    public void Empty_string_returns_empty()
    {
        Assert.Equal("", InjectableEmitter.SanitizeName(""));
    }
}
