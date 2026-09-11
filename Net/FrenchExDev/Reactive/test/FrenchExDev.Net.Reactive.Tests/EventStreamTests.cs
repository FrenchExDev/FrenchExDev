using FrenchExDev.Net.Reactive;

namespace FrenchExDev.Net.Reactive.Tests;

public sealed class EventStreamTests
{
    [Fact]
    public void Publish_subscribe_delivers_events()
    {
        // Arrange
        using var stream = new EventStream<int>();
        var received = new List<int>();
        using var sub = stream.Subscribe(received.Add);

        // Act
        stream.Publish(1);
        stream.Publish(2);
        stream.Publish(3);

        // Assert
        Assert.Equal([1, 2, 3], received);
    }

    [Fact]
    public void Complete_stops_stream()
    {
        // Arrange
        using var stream = new EventStream<int>();
        var received = new List<int>();
        var completed = false;
        using var sub = stream.Subscribe(received.Add, onCompleted: () => completed = true);

        // Act
        stream.Publish(1);
        stream.Complete();
        stream.Publish(2); // Should not be received

        // Assert
        Assert.Equal([1], received);
        Assert.True(completed);
    }

    [Fact]
    public void Error_propagates_exception()
    {
        // Arrange
        using var stream = new EventStream<int>();
        Exception? caught = null;
        using var sub = stream.Subscribe(_ => { }, onError: ex => caught = ex);

        // Act
        var expected = new InvalidOperationException("test");
        stream.Error(expected);

        // Assert
        Assert.Same(expected, caught);
    }

    [Fact]
    public void AsObservable_returns_observable()
    {
        // Arrange
        using var stream = new EventStream<int>();

        // Act
        var observable = stream.AsObservable();

        // Assert
        Assert.NotNull(observable);
    }

    [Fact]
    public void Multiple_subscribers_all_receive_events()
    {
        // Arrange
        using var stream = new EventStream<string>();
        var received1 = new List<string>();
        var received2 = new List<string>();
        using var sub1 = stream.Subscribe(received1.Add);
        using var sub2 = stream.Subscribe(received2.Add);

        // Act
        stream.Publish("hello");

        // Assert
        Assert.Equal(["hello"], received1);
        Assert.Equal(["hello"], received2);
    }

    [Fact]
    public void Disposing_subscription_stops_delivery()
    {
        // Arrange
        using var stream = new EventStream<int>();
        var received = new List<int>();
        var sub = stream.Subscribe(received.Add);

        // Act
        stream.Publish(1);
        sub.Dispose();
        stream.Publish(2);

        // Assert
        Assert.Equal([1], received);
    }
}
