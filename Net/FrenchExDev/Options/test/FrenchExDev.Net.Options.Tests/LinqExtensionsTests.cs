namespace FrenchExDev.Net.Options.Tests;

public class OptionLinq_Select
{
    [Fact]
    public void Select_maps_Some_value()
    {
        var result = from x in Option.Some(3)
                     select x * 2;
        result.ShouldBeSome(6);
    }

    [Fact]
    public void Select_on_None_returns_None()
    {
        var result = from x in Option.None<int>()
                     select x * 2;
        result.ShouldBeNone();
    }
}

public class OptionLinq_SelectMany
{
    [Fact]
    public void SelectMany_chains_two_Somes()
    {
        var result = from x in Option.Some(3)
                     from y in Option.Some(4)
                     select x + y;
        result.ShouldBeSome(7);
    }

    [Fact]
    public void SelectMany_short_circuits_on_first_None()
    {
        var result = from x in Option.None<int>()
                     from y in Option.Some(4)
                     select x + y;
        result.ShouldBeNone();
    }

    [Fact]
    public void SelectMany_short_circuits_on_second_None()
    {
        var result = from x in Option.Some(3)
                     from y in Option.None<int>()
                     select x + y;
        result.ShouldBeNone();
    }

    [Fact]
    public void SelectMany_chains_three_Somes()
    {
        var result = from x in Option.Some(1)
                     from y in Option.Some(2)
                     from z in Option.Some(3)
                     select x + y + z;
        result.ShouldBeSome(6);
    }
}

public class OptionLinq_Where
{
    [Fact]
    public void Where_filters_matching_value()
    {
        var result = from x in Option.Some(42)
                     where x > 0
                     select x;
        result.ShouldBeSome(42);
    }

    [Fact]
    public void Where_filters_out_non_matching_value()
    {
        var result = from x in Option.Some(-1)
                     where x > 0
                     select x;
        result.ShouldBeNone();
    }
}
