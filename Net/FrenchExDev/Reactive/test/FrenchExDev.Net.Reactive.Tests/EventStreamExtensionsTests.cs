using FrenchExDev.Net.Reactive;
using FrenchExDev.Net.Reactive.Testing;

namespace FrenchExDev.Net.Reactive.Tests;

public sealed class EventStreamExtensionsTests
{
    [Fact]
    public void Filter_keeps_matching_events()
    {
        // Arrange
        using var stream = new EventStream<int>();
        var filtered = stream.Filter(x => x > 2);
        var received = new List<int>();
        using var sub = filtered.Subscribe(received.Add);

        // Act
        stream.Publish(1);
        stream.Publish(3);
        stream.Publish(2);
        stream.Publish(5);

        // Assert
        Assert.Equal([3, 5], received);
    }

    [Fact]
    public void Map_projects_events()
    {
        // Arrange
        using var stream = new EventStream<int>();
        var mapped = stream.Map(x => x * 10);
        var received = new List<int>();
        using var sub = mapped.Subscribe(received.Add);

        // Act
        stream.Publish(1);
        stream.Publish(2);

        // Assert
        Assert.Equal([10, 20], received);
    }

    [Fact]
    public void Merge_combines_streams()
    {
        // Arrange
        using var stream1 = new EventStream<int>();
        using var stream2 = new EventStream<int>();
        var merged = stream1.Merge(stream2);
        var received = new List<int>();
        using var sub = merged.Subscribe(received.Add);

        // Act
        stream1.Publish(1);
        stream2.Publish(2);
        stream1.Publish(3);

        // Assert
        Assert.Equal([1, 2, 3], received);
    }

    [Fact]
    public void Buffer_by_count_groups_events()
    {
        // Arrange
        using var stream = new EventStream<int>();
        var buffered = stream.Buffer(2);
        var received = new List<IList<int>>();
        using var sub = buffered.Subscribe(received.Add);

        // Act
        stream.Publish(1);
        stream.Publish(2);
        stream.Publish(3);
        stream.Publish(4);

        // Assert
        Assert.Equal(2, received.Count);
        Assert.Equal([1, 2], received[0]);
        Assert.Equal([3, 4], received[1]);
    }

    [Fact]
    public void DistinctUntilChanged_suppresses_consecutive_duplicates()
    {
        // Arrange
        using var stream = new EventStream<int>();
        var distinct = stream.DistinctUntilChanged();
        var received = new List<int>();
        using var sub = distinct.Subscribe(received.Add);

        // Act
        stream.Publish(1);
        stream.Publish(1);
        stream.Publish(2);
        stream.Publish(2);
        stream.Publish(1);

        // Assert
        Assert.Equal([1, 2, 1], received);
    }

    [Fact]
    public void Take_limits_events()
    {
        // Arrange
        using var stream = new EventStream<int>();
        var taken = stream.Take(2);
        var received = new List<int>();
        using var sub = taken.Subscribe(received.Add);

        // Act
        stream.Publish(1);
        stream.Publish(2);
        stream.Publish(3);

        // Assert
        Assert.Equal([1, 2], received);
    }

    [Fact]
    public void Skip_ignores_initial_events()
    {
        // Arrange
        using var stream = new EventStream<int>();
        var skipped = stream.Skip(2);
        var received = new List<int>();
        using var sub = skipped.Subscribe(received.Add);

        // Act
        stream.Publish(1);
        stream.Publish(2);
        stream.Publish(3);
        stream.Publish(4);

        // Assert
        Assert.Equal([3, 4], received);
    }

    [Fact]
    public void TestEventStream_records_events()
    {
        // Arrange
        using var stream = new TestEventStream<string>();

        // Act
        stream.Publish("a");
        stream.Publish("b");

        // Assert
        Assert.Equal(["a", "b"], stream.Events);
    }

    [Fact]
    public void TestEventStream_clear_resets_events()
    {
        // Arrange
        using var stream = new TestEventStream<int>();
        stream.Publish(1);

        // Act
        stream.Clear();

        // Assert
        Assert.Empty(stream.Events);
    }

    [Fact]
    public void TestEventStream_subscribe_delivers_events()
    {
        // Arrange
        using var stream = new TestEventStream<int>();
        var received = new List<int>();
        using var sub = stream.Subscribe(received.Add);

        // Act
        stream.Publish(42);

        // Assert
        Assert.Equal([42], received);
        Assert.Equal([42], stream.Events);
    }

    [Fact]
    public void OfType_filters_by_type()
    {
        // Arrange
        using var stream = new EventStream<object>();
        var ints = stream.OfType<int>();
        var received = new List<int>();
        using var sub = ints.Subscribe(received.Add);

        // Act
        stream.Publish(1);
        stream.Publish("hello");
        stream.Publish(2);

        // Assert
        Assert.Equal([1, 2], received);
    }

    [Fact]
    public void Map_to_different_type()
    {
        // Arrange
        using var stream = new EventStream<int>();
        var mapped = stream.Map(x => x.ToString());
        var received = new List<string>();
        using var sub = mapped.Subscribe(received.Add);

        // Act
        stream.Publish(42);

        // Assert
        Assert.Equal(["42"], received);
    }
}
