namespace FrenchExDev.Net.Options.Tests;

public class Option_Some
{
    [Fact]
    public void IsSome_returns_true() =>
        Assert.True(Option.Some("hello").IsSome);

    [Fact]
    public void IsNone_returns_false() =>
        Assert.False(Option.Some("hello").IsNone);

    [Fact]
    public void Value_returns_the_wrapped_value() =>
        Assert.Equal("hello", Option.Some("hello").Value);

    [Fact]
    public void Some_with_null_throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => Option.Some<string>(null!));

    [Fact]
    public void ToString_returns_Some_representation() =>
        Assert.Equal("Some(42)", Option.Some(42).ToString());

    [Fact]
    public void Implicit_conversion_from_value_creates_Some()
    {
        Option<string> option = "hello";
        option.ShouldBeSome("hello");
    }
}

public class Option_None
{
    [Fact]
    public void IsSome_returns_false() =>
        Assert.False(Option.None<string>().IsSome);

    [Fact]
    public void IsNone_returns_true() =>
        Assert.True(Option.None<string>().IsNone);

    [Fact]
    public void Value_throws_InvalidOperationException() =>
        Assert.Throws<InvalidOperationException>(() => Option.None<string>().Value);

    [Fact]
    public void ToString_returns_None() =>
        Assert.Equal("None", Option.None<string>().ToString());
}

public class Option_From
{
    [Fact]
    public void From_non_null_reference_returns_Some() =>
        Option.From<string>("hello").ShouldBeSome("hello");

    [Fact]
    public void From_null_reference_returns_None() =>
        Option.From<string>(null).ShouldBeNone();

    [Fact]
    public void FromNullable_with_value_returns_Some() =>
        Option.FromNullable<int>(42).ShouldBeSome(42);

    [Fact]
    public void FromNullable_without_value_returns_None() =>
        Option.FromNullable<int>(null).ShouldBeNone();
}

public class Option_FromTry
{
    [Fact]
    public void FromTry_success_returns_Some() =>
        Option.FromTry(() => int.Parse("42")).ShouldBeSome(42);

    [Fact]
    public void FromTry_exception_returns_None() =>
        Option.FromTry(() => int.Parse("not-a-number")).ShouldBeNone();

    [Fact]
    public void FromTry_with_specific_exception_catches_only_that_type()
    {
        Option.FromTry<int, FormatException>(() => int.Parse("nope")).ShouldBeNone();
        Assert.Throws<OverflowException>(() =>
            Option.FromTry<int, FormatException>(() => int.Parse("99999999999999999999")));
    }
}

public class Option_Match
{
    [Fact]
    public void Match_on_Some_invokes_onSome() =>
        Assert.Equal(5, Option.Some("hello").Match(s => s.Length, () => -1));

    [Fact]
    public void Match_on_None_invokes_onNone() =>
        Assert.Equal(-1, Option.None<string>().Match(s => s.Length, () => -1));
}

public class Option_Switch
{
    [Fact]
    public void Switch_on_Some_invokes_onSome()
    {
        var called = false;
        Option.Some("hello").Switch(_ => called = true, () => { });
        Assert.True(called);
    }

    [Fact]
    public void Switch_on_None_invokes_onNone()
    {
        var called = false;
        Option.None<string>().Switch(_ => { }, () => called = true);
        Assert.True(called);
    }
}

public class Option_Equality
{
    [Fact]
    public void Two_Somes_with_same_value_are_equal() =>
        Assert.Equal(Option.Some(42), Option.Some(42));

    [Fact]
    public void Two_Somes_with_different_values_are_not_equal() =>
        Assert.NotEqual(Option.Some(42), Option.Some(99));

    [Fact]
    public void Two_Nones_are_equal() =>
        Assert.Equal(Option.None<int>(), Option.None<int>());

    [Fact]
    public void Some_and_None_are_not_equal() =>
        Assert.NotEqual(Option.Some(42), Option.None<int>());

    [Fact]
    public void HashCode_is_consistent_for_same_values()
    {
        var set = new HashSet<Option<int>> { Option.Some(1), Option.Some(1), Option.None<int>() };
        Assert.Equal(2, set.Count);
    }
}
