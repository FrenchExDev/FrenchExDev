namespace FrenchExDev.Net.Options.Tests;

public class OptionExtensions_Map
{
    [Fact]
    public void Map_on_Some_applies_mapper() =>
        Option.Some("hello").Map(s => s.Length).ShouldBeSome(5);

    [Fact]
    public void Map_on_None_returns_None() =>
        Option.None<string>().Map(s => s.Length).ShouldBeNone();
}

public class OptionExtensions_Bind
{
    private static Option<int> ParseInt(string s) =>
        int.TryParse(s, out var v) ? Option.Some(v) : Option.None<int>();

    [Fact]
    public void Bind_on_Some_applies_binder() =>
        Option.Some("42").Bind(ParseInt).ShouldBeSome(42);

    [Fact]
    public void Bind_on_Some_with_failing_binder_returns_None() =>
        Option.Some("nope").Bind(ParseInt).ShouldBeNone();

    [Fact]
    public void Bind_on_None_returns_None_without_invoking_binder()
    {
        var called = false;
        Option.None<string>().Bind(s => { called = true; return ParseInt(s); }).ShouldBeNone();
        Assert.False(called);
    }

    [Fact]
    public void Then_is_alias_for_Bind() =>
        Assert.Equal(
            Option.Some("42").Bind(ParseInt),
            Option.Some("42").Then(ParseInt));
}

public class OptionExtensions_Where
{
    [Fact]
    public void Where_on_Some_passes_predicate_returns_Some() =>
        Option.Some(42).Where(x => x > 0).ShouldBeSome(42);

    [Fact]
    public void Where_on_Some_fails_predicate_returns_None() =>
        Option.Some(-1).Where(x => x > 0).ShouldBeNone();

    [Fact]
    public void Where_on_None_returns_None() =>
        Option.None<int>().Where(x => x > 0).ShouldBeNone();

    [Fact]
    public void Filter_is_alias_for_Where() =>
        Assert.Equal(
            Option.Some(42).Where(x => x > 0),
            Option.Some(42).Filter(x => x > 0));
}

public class OptionExtensions_Tap
{
    [Fact]
    public void Tap_on_Some_invokes_action_and_returns_same_option()
    {
        var captured = "";
        var result = Option.Some("hello").Tap(s => captured = s);
        result.ShouldBeSome("hello");
        Assert.Equal("hello", captured);
    }

    [Fact]
    public void Tap_on_None_does_not_invoke_action()
    {
        var called = false;
        Option.None<string>().Tap(_ => called = true).ShouldBeNone();
        Assert.False(called);
    }

    [Fact]
    public void TapNone_on_None_invokes_action()
    {
        var called = false;
        Option.None<string>().TapNone(() => called = true).ShouldBeNone();
        Assert.True(called);
    }

    [Fact]
    public void TapNone_on_Some_does_not_invoke_action()
    {
        var called = false;
        Option.Some("hello").TapNone(() => called = true).ShouldBeSome("hello");
        Assert.False(called);
    }
}

public class OptionExtensions_OrDefault
{
    [Fact]
    public void OrDefault_on_Some_returns_value() =>
        Assert.Equal("hello", Option.Some("hello").OrDefault("fallback"));

    [Fact]
    public void OrDefault_on_None_returns_fallback() =>
        Assert.Equal("fallback", Option.None<string>().OrDefault("fallback"));
}

public class OptionExtensions_OrElse
{
    [Fact]
    public void OrElse_on_Some_does_not_invoke_factory() =>
        Assert.Equal("hello", Option.Some("hello").OrElse(() => throw new Exception("should not be called")));

    [Fact]
    public void OrElse_on_None_invokes_factory() =>
        Assert.Equal("fallback", Option.None<string>().OrElse(() => "fallback"));
}

public class OptionExtensions_Or
{
    [Fact]
    public void Or_on_Some_returns_original() =>
        Option.Some(1).Or(Option.Some(2)).ShouldBeSome(1);

    [Fact]
    public void Or_on_None_returns_alternative() =>
        Option.None<int>().Or(Option.Some(2)).ShouldBeSome(2);

    [Fact]
    public void Or_lazy_on_Some_does_not_invoke_factory() =>
        Option.Some(1).Or(() => throw new Exception("nope")).ShouldBeSome(1);

    [Fact]
    public void Or_lazy_on_None_invokes_factory() =>
        Option.None<int>().Or(() => Option.Some(2)).ShouldBeSome(2);
}

public class OptionExtensions_ToNullable
{
    [Fact]
    public void ToNullable_on_Some_returns_value() =>
        Assert.Equal("hello", Option.Some("hello").ToNullable());

    [Fact]
    public void ToNullable_on_None_returns_null() =>
        Assert.Null(Option.None<string>().ToNullable());

    [Fact]
    public void ToNullableStruct_on_Some_returns_value() =>
        Assert.Equal(42, Option.Some(42).ToNullableStruct());

    [Fact]
    public void ToNullableStruct_on_None_returns_null() =>
        Assert.Null(Option.None<int>().ToNullableStruct());
}

public class OptionExtensions_Zip
{
    [Fact]
    public void Zip_both_Some_returns_tuple() =>
        Option.Some(1).Zip(Option.Some("a")).ShouldBeSome((1, "a"));

    [Fact]
    public void Zip_first_None_returns_None() =>
        Option.None<int>().Zip(Option.Some("a")).ShouldBeNone();

    [Fact]
    public void Zip_second_None_returns_None() =>
        Option.Some(1).Zip(Option.None<string>()).ShouldBeNone();

    [Fact]
    public void Zip_with_selector_combines_values() =>
        Option.Some(2).Zip(Option.Some(3), (a, b) => a * b).ShouldBeSome(6);
}

public class OptionExtensions_Contains
{
    [Fact]
    public void Contains_predicate_on_Some_matching_returns_true() =>
        Assert.True(Option.Some(42).Contains(x => x > 0));

    [Fact]
    public void Contains_predicate_on_Some_not_matching_returns_false() =>
        Assert.False(Option.Some(-1).Contains(x => x > 0));

    [Fact]
    public void Contains_predicate_on_None_returns_false() =>
        Assert.False(Option.None<int>().Contains(x => x > 0));

    [Fact]
    public void Contains_value_on_Some_equal_returns_true() =>
        Assert.True(Option.Some(42).Contains(42));

    [Fact]
    public void Contains_value_on_Some_not_equal_returns_false() =>
        Assert.False(Option.Some(42).Contains(99));
}
