namespace FrenchExDev.Net.Guard.Tests;

public class GuardEnsure_That
{
    [Fact]
    public void That_true_does_not_throw() =>
        Guard.Ensure.That(true, "Should not throw");

    [Fact]
    public void That_false_throws_InvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Guard.Ensure.That(false, "Invariant violated"));
        Assert.Equal("Invariant violated", ex.Message);
    }

    [Fact]
    public void That_lazy_message_false_invokes_factory()
    {
        var factoryCalled = false;
        Assert.Throws<InvalidOperationException>(() =>
            Guard.Ensure.That(false, () => { factoryCalled = true; return "Lazy message"; }));
        Assert.True(factoryCalled);
    }

    [Fact]
    public void That_lazy_message_true_does_not_invoke_factory()
    {
        var factoryCalled = false;
        Guard.Ensure.That(true, () => { factoryCalled = true; return "Lazy message"; });
        Assert.False(factoryCalled);
    }
}

public class GuardEnsure_NotNull
{
    [Fact]
    public void NotNull_with_value_returns_value() =>
        Assert.Equal("hello", Guard.Ensure.NotNull("hello", "unexpected null"));

    [Fact]
    public void NotNull_with_null_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Guard.Ensure.NotNull<string>(null, "Should have been set by Init()"));
        Assert.Equal("Should have been set by Init()", ex.Message);
    }
}

public class GuardEnsure_NotAlreadyDone
{
    [Fact]
    public void NotAlreadyDone_false_does_not_throw() =>
        Guard.Ensure.NotAlreadyDone(false, "Build");

    [Fact]
    public void NotAlreadyDone_true_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Guard.Ensure.NotAlreadyDone(true, "Build"));
        Assert.Equal("Build has already been performed.", ex.Message);
    }
}
