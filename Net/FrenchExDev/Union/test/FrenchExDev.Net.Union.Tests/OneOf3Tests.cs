using System.Globalization;
using Xunit;

namespace FrenchExDev.Net.Union.Tests;

public sealed class OneOf3Tests
{
    // --- Creating from each type ---

    [Fact]
    public void From_T1_creates_union_holding_T1()
    {
        var union = OneOf<string, int, double>.From("hello");

        Assert.True(union.IsT1);
        Assert.False(union.IsT2);
        Assert.False(union.IsT3);
        Assert.Equal("hello", union.AsT1);
    }

    [Fact]
    public void From_T2_creates_union_holding_T2()
    {
        var union = OneOf<string, int, double>.From(42);

        Assert.False(union.IsT1);
        Assert.True(union.IsT2);
        Assert.False(union.IsT3);
        Assert.Equal(42, union.AsT2);
    }

    [Fact]
    public void From_T3_creates_union_holding_T3()
    {
        var union = OneOf<string, int, double>.From(3.14);

        Assert.False(union.IsT1);
        Assert.False(union.IsT2);
        Assert.True(union.IsT3);
        Assert.Equal(3.14, union.AsT3);
    }

    // --- AsT* on wrong type throws ---

    [Fact]
    public void AsT1_on_T2_throws()
    {
        var union = OneOf<string, int, double>.From(42);
        Assert.Throws<InvalidOperationException>(() => union.AsT1);
    }

    [Fact]
    public void AsT2_on_T3_throws()
    {
        var union = OneOf<string, int, double>.From(3.14);
        Assert.Throws<InvalidOperationException>(() => union.AsT2);
    }

    [Fact]
    public void AsT3_on_T1_throws()
    {
        var union = OneOf<string, int, double>.From("hello");
        Assert.Throws<InvalidOperationException>(() => union.AsT3);
    }

    // --- Match ---

    [Fact]
    public void Match_invokes_T1_branch()
    {
        var union = OneOf<string, int, double>.From("hello");

        var result = union.Match(
            s => $"s:{s}",
            i => $"i:{i}",
            d => $"d:{d}");

        Assert.Equal("s:hello", result);
    }

    [Fact]
    public void Match_invokes_T2_branch()
    {
        var union = OneOf<string, int, double>.From(42);

        var result = union.Match(
            s => $"s:{s}",
            i => $"i:{i}",
            d => $"d:{d}");

        Assert.Equal("i:42", result);
    }

    [Fact]
    public void Match_invokes_T3_branch()
    {
        var union = OneOf<string, int, double>.From(3.14);

        var result = union.Match(
            s => $"s:{s}",
            i => $"i:{i}",
            d => string.Format(CultureInfo.InvariantCulture, "d:{0}", d));

        Assert.Equal("d:3.14", result);
    }

    // --- Switch ---

    [Fact]
    public void Switch_invokes_T1_action()
    {
        var union = OneOf<string, int, double>.From("hello");
        string? captured = null;

        union.Switch(
            s => captured = s,
            _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException());

        Assert.Equal("hello", captured);
    }

    [Fact]
    public void Switch_invokes_T2_action()
    {
        var union = OneOf<string, int, double>.From(42);
        int? captured = null;

        union.Switch(
            _ => throw new InvalidOperationException(),
            i => captured = i,
            _ => throw new InvalidOperationException());

        Assert.Equal(42, captured);
    }

    [Fact]
    public void Switch_invokes_T3_action()
    {
        var union = OneOf<string, int, double>.From(3.14);
        double? captured = null;

        union.Switch(
            _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(),
            d => captured = d);

        Assert.Equal(3.14, captured);
    }

    // --- Implicit conversions ---

    [Fact]
    public void Implicit_conversion_from_T1()
    {
        OneOf<string, int, double> union = "implicit";
        Assert.True(union.IsT1);
    }

    [Fact]
    public void Implicit_conversion_from_T2()
    {
        OneOf<string, int, double> union = 99;
        Assert.True(union.IsT2);
    }

    [Fact]
    public void Implicit_conversion_from_T3()
    {
        OneOf<string, int, double> union = 2.72;
        Assert.True(union.IsT3);
    }

    // --- TryGet ---

    [Fact]
    public void TryGet_matching_returns_true()
    {
        var union = OneOf<string, int, double>.From(42);
        Assert.True(union.TryGet<int>(out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGet_non_matching_returns_false()
    {
        var union = OneOf<string, int, double>.From(42);
        Assert.False(union.TryGet<string>(out _));
    }

    // --- Equality ---

    [Fact]
    public void Equals_same_value_same_type()
    {
        var a = OneOf<string, int, double>.From("hello");
        var b = OneOf<string, int, double>.From("hello");
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_different_values()
    {
        var a = OneOf<string, int, double>.From(1);
        var b = OneOf<string, int, double>.From(2);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_different_type_slots()
    {
        var a = OneOf<string, int, double>.From("1");
        var b = OneOf<string, int, double>.From(1);
        Assert.NotEqual(a, b);
    }

    // --- ToString ---

    [Fact]
    public void ToString_T1() => Assert.Equal("T1(hello)", OneOf<string, int, double>.From("hello").ToString());

    [Fact]
    public void ToString_T2() => Assert.Equal("T2(42)", OneOf<string, int, double>.From(42).ToString());

    [Fact]
    public void ToString_T3() => Assert.Equal("T3(3.14)", OneOf<string, int, double>.From(3.14).ToString());
}
