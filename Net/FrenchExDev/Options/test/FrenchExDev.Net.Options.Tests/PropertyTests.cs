namespace FrenchExDev.Net.Options.Tests;

using CsCheck;

file static class Gens
{
    internal static readonly Gen<string> Value =
        Gen.String[1, 20];

    internal static readonly Gen<Option<string>> SomeOption =
        Value.Select(Option.Some);

    internal static readonly Gen<Option<string>> NoneOption =
        Gen.Const(Option.None<string>());

    internal static readonly Gen<Option<string>> AnyOption =
        Gen.Frequency(
            (3, SomeOption),
            (1, NoneOption));
}

public class Option_FunctorLaws
{
    [Fact]
    public void Map_identity() =>
        Gens.AnyOption.Sample(option =>
            Assert.Equal(option, option.Map(x => x)));

    [Fact]
    public void Map_composition() =>
        Gens.AnyOption.Sample(option =>
        {
            Func<string, int> f = s => s.Length;
            Func<int, string> g = i => i.ToString();
            Assert.Equal(
                option.Map(f).Map(g),
                option.Map(x => g(f(x))));
        });
}

public class Option_MonadLaws
{
    private static Option<int> LengthOption(string s) =>
        Option.Some(s.Length);

    private static Option<string> ToStringOption(int i) =>
        Option.Some(i.ToString());

    [Fact]
    public void Bind_left_identity() =>
        Gens.Value.Sample(value =>
            Assert.Equal(
                Option.Some(value).Bind(LengthOption),
                LengthOption(value)));

    [Fact]
    public void Bind_right_identity() =>
        Gens.AnyOption.Sample(option =>
            Assert.Equal(
                option,
                option.Bind(x => Option.Some(x))));

    [Fact]
    public void Bind_associativity() =>
        Gens.AnyOption.Sample(option =>
            Assert.Equal(
                option.Bind(LengthOption).Bind(ToStringOption),
                option.Bind(x => LengthOption(x).Bind(ToStringOption))));
}

public class Option_TapProperties
{
    [Fact]
    public void Tap_on_Some_always_invokes_action() =>
        Gens.SomeOption.Sample(option =>
        {
            var called = false;
            option.Tap(_ => called = true);
            Assert.True(called);
        });

    [Fact]
    public void Tap_on_None_never_invokes_action() =>
        Gens.NoneOption.Sample(option =>
        {
            var called = false;
            option.Tap(_ => called = true);
            Assert.False(called);
        });

    [Fact]
    public void Tap_preserves_value() =>
        Gens.AnyOption.Sample(option =>
            Assert.Equal(option, option.Tap(_ => { })));
}

public class Option_WhereProperties
{
    [Fact]
    public void Where_always_true_is_identity_on_Some() =>
        Gens.SomeOption.Sample(option =>
            Assert.Equal(option, option.Where(_ => true)));

    [Fact]
    public void Where_always_false_returns_None() =>
        Gens.AnyOption.Sample(option =>
            Assert.True(option.Where(_ => false).IsNone));

    [Fact]
    public void Where_on_None_is_always_None() =>
        Gens.NoneOption.Sample(option =>
            Assert.True(option.Where(_ => true).IsNone));
}

public class Option_OrDefaultProperties
{
    [Fact]
    public void OrDefault_on_Some_returns_value() =>
        Gens.SomeOption.Sample(option =>
            Assert.Equal(option.Value, option.OrDefault("fallback")));

    [Fact]
    public void OrDefault_on_None_returns_fallback() =>
        Gens.NoneOption.Sample(option =>
            Assert.Equal("fallback", option.OrDefault("fallback")));
}
