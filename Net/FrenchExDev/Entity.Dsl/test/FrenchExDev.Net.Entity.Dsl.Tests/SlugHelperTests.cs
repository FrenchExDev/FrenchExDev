using FrenchExDev.Net.Entity.Dsl.Abstractions;
using Xunit;

namespace FrenchExDev.Net.Entity.Dsl.Tests;

public class SlugHelperTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("My First Blog Post", "my-first-blog-post")]
    [InlineData("  spaces  everywhere  ", "spaces-everywhere")]
    [InlineData("UPPERCASE", "uppercase")]
    [InlineData("already-slug", "already-slug")]
    [InlineData("multiple---dashes", "multiple-dashes")]
    public void ToSlug_basic_cases(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.ToSlug(input));
    }

    [Theory]
    [InlineData("Caf\u00e9 au lait", "cafe-au-lait")]
    [InlineData("\u00fcber cool", "uber-cool")]
    [InlineData("jalape\u00f1o", "jalapeno")]
    public void ToSlug_transliterates_diacritics(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.ToSlug(input));
    }

    [Fact]
    public void ToSlug_without_transliteration()
    {
        var result = SlugHelper.ToSlug("Caf\u00e9", transliterate: false);
        Assert.DoesNotContain("cafe", result);
    }

    [Fact]
    public void ToSlug_without_lowercase()
    {
        var result = SlugHelper.ToSlug("Hello World", lowercase: false);
        Assert.Contains("Hello", result);
    }

    [Fact]
    public void ToSlug_custom_separator()
    {
        var result = SlugHelper.ToSlug("Hello World", separator: "_");
        Assert.Equal("hello_world", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ToSlug_empty_and_whitespace(string? input)
    {
        Assert.Equal(string.Empty, SlugHelper.ToSlug(input ?? ""));
    }

    [Fact]
    public void ToSlug_special_characters_removed()
    {
        var result = SlugHelper.ToSlug("Hello! @World# $2025");
        Assert.Equal("hello-world-2025", result);
    }
}
