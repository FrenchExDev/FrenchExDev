using FrenchExDev.Net.Union.Testing;
using Xunit;

namespace FrenchExDev.Net.Union.Tests;

public sealed class OneOf2Tests
{
    // --- Creating from each type ---

    [Fact]
    public void From_T1_creates_union_holding_T1()
    {
        var union = OneOf<string, int>.From("hello");

        Assert.True(union.IsT1);
        Assert.False(union.IsT2);
        Assert.Equal("hello", union.AsT1);
    }

    [Fact]
    public void From_T2_creates_union_holding_T2()
    {
        var union = OneOf<string, int>.From(42);

        Assert.True(union.IsT2);
        Assert.False(union.IsT1);
        Assert.Equal(42, union.AsT2);
    }

    // --- IsT* exclusivity ---

    [Fact]
    public void IsT1_and_IsT2_are_mutually_exclusive()
    {
        var unionT1 = OneOf<string, int>.From("a");
        var unionT2 = OneOf<string, int>.From(1);

        Assert.True(unionT1.IsT1);
        Assert.False(unionT1.IsT2);
        Assert.False(unionT2.IsT1);
        Assert.True(unionT2.IsT2);
    }

    // --- AsT* on wrong type throws ---

    [Fact]
    public void AsT1_on_T2_throws_InvalidOperationException()
    {
        var union = OneOf<string, int>.From(42);

        var ex = Assert.Throws<InvalidOperationException>(() => union.AsT1);
        Assert.Contains("T1", ex.Message);
    }

    [Fact]
    public void AsT2_on_T1_throws_InvalidOperationException()
    {
        var union = OneOf<string, int>.From("hello");

        var ex = Assert.Throws<InvalidOperationException>(() => union.AsT2);
        Assert.Contains("T2", ex.Message);
    }

    // --- Match invokes correct branch ---

    [Fact]
    public void Match_invokes_T1_branch_when_holding_T1()
    {
        var union = OneOf<string, int>.From("hello");

        var result = union.Match(
            s => $"string:{s}",
            i => $"int:{i}");

        Assert.Equal("string:hello", result);
    }

    [Fact]
    public void Match_invokes_T2_branch_when_holding_T2()
    {
        var union = OneOf<string, int>.From(42);

        var result = union.Match(
            s => $"string:{s}",
            i => $"int:{i}");

        Assert.Equal("int:42", result);
    }

    // --- Switch invokes correct action ---

    [Fact]
    public void Switch_invokes_T1_action_when_holding_T1()
    {
        var union = OneOf<string, int>.From("hello");
        string? captured = null;

        union.Switch(
            s => captured = s,
            _ => throw new InvalidOperationException("Should not be called"));

        Assert.Equal("hello", captured);
    }

    [Fact]
    public void Switch_invokes_T2_action_when_holding_T2()
    {
        var union = OneOf<string, int>.From(42);
        int? captured = null;

        union.Switch(
            _ => throw new InvalidOperationException("Should not be called"),
            i => captured = i);

        Assert.Equal(42, captured);
    }

    // --- Implicit conversions ---

    [Fact]
    public void Implicit_conversion_from_T1()
    {
        OneOf<string, int> union = "implicit";

        Assert.True(union.IsT1);
        Assert.Equal("implicit", union.AsT1);
    }

    [Fact]
    public void Implicit_conversion_from_T2()
    {
        OneOf<string, int> union = 99;

        Assert.True(union.IsT2);
        Assert.Equal(99, union.AsT2);
    }

    // --- TryGet matching / non-matching ---

    [Fact]
    public void TryGet_returns_true_and_value_when_matching()
    {
        var union = OneOf<string, int>.From("test");

        var success = union.TryGet<string>(out var value);

        Assert.True(success);
        Assert.Equal("test", value);
    }

    [Fact]
    public void TryGet_returns_false_when_not_matching()
    {
        var union = OneOf<string, int>.From("test");

        var success = union.TryGet<int>(out _);

        Assert.False(success);
    }

    // --- Equality ---

    [Fact]
    public void Equals_returns_true_for_same_value_and_type()
    {
        var a = OneOf<string, int>.From("hello");
        var b = OneOf<string, int>.From("hello");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_returns_false_for_different_values_same_type()
    {
        var a = OneOf<string, int>.From("hello");
        var b = OneOf<string, int>.From("world");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_returns_false_for_different_types()
    {
        var a = OneOf<string, int>.From("42");
        var b = OneOf<string, int>.From(42);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_returns_false_for_null()
    {
        var union = OneOf<string, int>.From("hello");

        Assert.False(union.Equals(null));
    }

    [Fact]
    public void Equals_returns_true_for_same_reference()
    {
        var union = OneOf<string, int>.From("hello");

        Assert.True(union.Equals(union));
    }

    // --- ToString ---

    [Fact]
    public void ToString_includes_type_index_and_value_for_T1()
    {
        var union = OneOf<string, int>.From("hello");

        Assert.Equal("T1(hello)", union.ToString());
    }

    [Fact]
    public void ToString_includes_type_index_and_value_for_T2()
    {
        var union = OneOf<string, int>.From(42);

        Assert.Equal("T2(42)", union.ToString());
    }

    // --- Testing assertions ---

    [Fact]
    public void ShouldBeT1_returns_value_when_T1()
    {
        var union = OneOf<string, int>.From("hello");

        var value = union.ShouldBeT1();

        Assert.Equal("hello", value);
    }

    [Fact]
    public void ShouldBeT1_throws_when_T2()
    {
        var union = OneOf<string, int>.From(42);

        Assert.Throws<InvalidOperationException>(() => union.ShouldBeT1());
    }

    [Fact]
    public void ShouldBeT2_returns_value_when_T2()
    {
        var union = OneOf<string, int>.From(42);

        var value = union.ShouldBeT2();

        Assert.Equal(42, value);
    }

    [Fact]
    public void ShouldBeT2_throws_when_T1()
    {
        var union = OneOf<string, int>.From("hello");

        Assert.Throws<InvalidOperationException>(() => union.ShouldBeT2());
    }
}
