namespace FrenchExDev.Net.Options.Tests;

public class OptionAsyncExtensions_MapAsync
{
    [Fact]
    public async Task MapAsync_on_Some_applies_async_mapper()
    {
        var result = await Option.Some("hello").MapAsync(s => Task.FromResult(s.Length));
        result.ShouldBeSome(5);
    }

    [Fact]
    public async Task MapAsync_on_None_returns_None()
    {
        var result = await Option.None<string>().MapAsync(s => Task.FromResult(s.Length));
        result.ShouldBeNone();
    }

    [Fact]
    public async Task MapAsync_pipeline_sync_mapper()
    {
        var result = await Task.FromResult(Option.Some("hello")).MapAsync(s => s.Length);
        result.ShouldBeSome(5);
    }

    [Fact]
    public async Task MapAsync_pipeline_async_mapper()
    {
        var result = await Task.FromResult(Option.Some("hello"))
            .MapAsync(s => Task.FromResult(s.Length));
        result.ShouldBeSome(5);
    }
}

public class OptionAsyncExtensions_BindAsync
{
    private static Task<Option<int>> ParseIntAsync(string s) =>
        Task.FromResult(int.TryParse(s, out var v) ? Option.Some(v) : Option.None<int>());

    [Fact]
    public async Task BindAsync_on_Some_applies_async_binder()
    {
        var result = await Option.Some("42").BindAsync(ParseIntAsync);
        result.ShouldBeSome(42);
    }

    [Fact]
    public async Task BindAsync_on_None_returns_None()
    {
        var result = await Option.None<string>().BindAsync(ParseIntAsync);
        result.ShouldBeNone();
    }

    [Fact]
    public async Task BindAsync_pipeline_sync_binder()
    {
        static Option<int> ParseInt(string s) =>
            int.TryParse(s, out var v) ? Option.Some(v) : Option.None<int>();

        var result = await Task.FromResult(Option.Some("42")).BindAsync(ParseInt);
        result.ShouldBeSome(42);
    }

    [Fact]
    public async Task BindAsync_pipeline_async_binder()
    {
        var result = await Task.FromResult(Option.Some("42")).BindAsync(ParseIntAsync);
        result.ShouldBeSome(42);
    }
}

public class OptionAsyncExtensions_TapAsync
{
    [Fact]
    public async Task TapAsync_on_Some_invokes_async_action()
    {
        var captured = "";
        var result = await Option.Some("hello").TapAsync(s => { captured = s; return Task.CompletedTask; });
        result.ShouldBeSome("hello");
        Assert.Equal("hello", captured);
    }

    [Fact]
    public async Task TapAsync_on_None_does_not_invoke()
    {
        var called = false;
        var result = await Option.None<string>().TapAsync(_ => { called = true; return Task.CompletedTask; });
        result.ShouldBeNone();
        Assert.False(called);
    }
}

public class OptionAsyncExtensions_Pipeline
{
    [Fact]
    public async Task Full_async_pipeline_without_intermediate_awaits()
    {
        var result = await Task.FromResult(Option.Some("42"))
            .BindAsync(s => int.TryParse(s, out var v) ? Option.Some(v) : Option.None<int>())
            .MapAsync(x => x * 2)
            .WhereAsync(x => x > 0)
            .MapAsync(x => x.ToString());

        result.ShouldBeSome("84");
    }

    [Fact]
    public async Task Pipeline_short_circuits_on_None()
    {
        var mapCalled = false;
        var result = await Task.FromResult(Option.None<string>())
            .MapAsync(s => { mapCalled = true; return s.Length; })
            .MapAsync(x => x * 2);

        result.ShouldBeNone();
        Assert.False(mapCalled);
    }

    [Fact]
    public async Task OrDefaultAsync_on_pipeline()
    {
        var value = await Task.FromResult(Option.None<int>()).OrDefaultAsync(42);
        Assert.Equal(42, value);
    }

    [Fact]
    public async Task MatchAsync_on_pipeline()
    {
        var result = await Task.FromResult(Option.Some("hello"))
            .MatchAsync(s => s.Length, () => -1);
        Assert.Equal(5, result);
    }
}
