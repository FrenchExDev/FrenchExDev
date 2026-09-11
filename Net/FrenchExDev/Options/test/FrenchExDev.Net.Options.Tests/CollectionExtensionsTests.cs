namespace FrenchExDev.Net.Options.Tests;

public class OptionCollections_Values
{
    [Fact]
    public void Values_extracts_Some_values()
    {
        var options = new[] { Option.Some(1), Option.None<int>(), Option.Some(3) };
        Assert.Equal(new[] { 1, 3 }, options.Values());
    }

    [Fact]
    public void Values_on_all_None_returns_empty()
    {
        var options = new[] { Option.None<int>(), Option.None<int>() };
        Assert.Empty(options.Values());
    }
}

public class OptionCollections_FirstOrNone
{
    [Fact]
    public void FirstOrNone_returns_first_Some()
    {
        var options = new[] { Option.None<int>(), Option.Some(2), Option.Some(3) };
        options.FirstOrNone().ShouldBeSome(2);
    }

    [Fact]
    public void FirstOrNone_returns_None_when_all_None()
    {
        var options = new[] { Option.None<int>(), Option.None<int>() };
        options.FirstOrNone().ShouldBeNone();
    }

    [Fact]
    public void FirstOrNone_on_source_with_predicate_matching()
    {
        var numbers = new[] { 1, 2, 3, 4 };
        numbers.FirstOrNone(x => x > 2).ShouldBeSome(3);
    }

    [Fact]
    public void FirstOrNone_on_source_with_predicate_not_matching()
    {
        var numbers = new[] { 1, 2, 3 };
        numbers.FirstOrNone(x => x > 10).ShouldBeNone();
    }
}

public class OptionCollections_SingleOrNone
{
    [Fact]
    public void SingleOrNone_returns_Some_for_exactly_one_match()
    {
        var numbers = new[] { 1, 2, 3 };
        numbers.SingleOrNone(x => x == 2).ShouldBeSome(2);
    }

    [Fact]
    public void SingleOrNone_returns_None_for_no_match()
    {
        var numbers = new[] { 1, 2, 3 };
        numbers.SingleOrNone(x => x == 99).ShouldBeNone();
    }

    [Fact]
    public void SingleOrNone_returns_None_for_multiple_matches()
    {
        var numbers = new[] { 1, 2, 2, 3 };
        numbers.SingleOrNone(x => x == 2).ShouldBeNone();
    }
}

public class OptionCollections_GetValueOrNone
{
    [Fact]
    public void GetValueOrNone_found_returns_Some()
    {
        var dict = new Dictionary<string, int> { ["key"] = 42 };
        dict.GetValueOrNone("key").ShouldBeSome(42);
    }

    [Fact]
    public void GetValueOrNone_not_found_returns_None()
    {
        var dict = new Dictionary<string, int> { ["key"] = 42 };
        dict.GetValueOrNone("missing").ShouldBeNone();
    }
}

public class OptionCollections_Sequence
{
    [Fact]
    public void Sequence_all_Some_returns_Some_with_all_values()
    {
        var options = new[] { Option.Some(1), Option.Some(2), Option.Some(3) };
        var result = options.Sequence();
        var values = result.ShouldBeSome();
        Assert.Equal(new[] { 1, 2, 3 }, values);
    }

    [Fact]
    public void Sequence_one_None_returns_None()
    {
        var options = new[] { Option.Some(1), Option.None<int>(), Option.Some(3) };
        options.Sequence().ShouldBeNone();
    }

    [Fact]
    public void Sequence_empty_returns_Some_with_empty_list()
    {
        var options = Array.Empty<Option<int>>();
        var result = options.Sequence();
        var values = result.ShouldBeSome();
        Assert.Empty(values);
    }
}

public class OptionCollections_Traverse
{
    [Fact]
    public void Traverse_all_succeed_returns_Some()
    {
        var strings = new[] { "1", "2", "3" };
        var result = strings.Traverse(s =>
            int.TryParse(s, out var v) ? Option.Some(v) : Option.None<int>());
        var values = result.ShouldBeSome();
        Assert.Equal(new[] { 1, 2, 3 }, values);
    }

    [Fact]
    public void Traverse_one_fails_returns_None()
    {
        var strings = new[] { "1", "oops", "3" };
        var result = strings.Traverse(s =>
            int.TryParse(s, out var v) ? Option.Some(v) : Option.None<int>());
        result.ShouldBeNone();
    }
}
