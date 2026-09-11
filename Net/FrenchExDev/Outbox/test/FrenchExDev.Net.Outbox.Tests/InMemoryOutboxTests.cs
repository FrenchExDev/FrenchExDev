using FrenchExDev.Net.Outbox;
using FrenchExDev.Net.Outbox.Testing;

using Xunit;

namespace FrenchExDev.Net.Outbox.Tests;

public sealed class InMemoryOutboxTests
{
    [Fact]
    public void Messages_is_initially_empty()
    {
        var outbox = new InMemoryOutbox();
        Assert.Empty(outbox.Messages);
    }

    [Fact]
    public async Task StoreAsync_adds_message()
    {
        var outbox = new InMemoryOutbox();
        var message = new OutboxMessage { Type = "TestEvent", Payload = "{}" };

        await outbox.StoreAsync(message);

        Assert.Single(outbox.Messages);
        Assert.Contains(message, outbox.Messages);
    }

    [Fact]
    public async Task StoreAsync_adds_multiple_messages()
    {
        var outbox = new InMemoryOutbox();
        var message1 = new OutboxMessage { Type = "Event1", Payload = "{\"a\":1}" };
        var message2 = new OutboxMessage { Type = "Event2", Payload = "{\"b\":2}" };

        await outbox.StoreAsync(message1);
        await outbox.StoreAsync(message2);

        Assert.Equal(2, outbox.Messages.Count);
    }

    [Fact]
    public async Task StoreAsync_throws_on_null_message()
    {
        var outbox = new InMemoryOutbox();
        await Assert.ThrowsAsync<ArgumentNullException>(() => outbox.StoreAsync(null!));
    }

    [Fact]
    public async Task StoreAsync_is_thread_safe()
    {
        var outbox = new InMemoryOutbox();
        const int count = 1000;

        var tasks = Enumerable.Range(0, count)
            .Select(i => outbox.StoreAsync(new OutboxMessage { Type = $"Event{i}" }))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(count, outbox.Messages.Count);
    }

    [Fact]
    public async Task StoreAsync_preserves_message_properties()
    {
        var outbox = new InMemoryOutbox();
        var id = Guid.NewGuid();
        var message = new OutboxMessage
        {
            Id = id,
            Type = "OrderPlaced",
            Payload = "{\"orderId\":42}",
        };

        await outbox.StoreAsync(message);

        var stored = outbox.Messages.Single();
        Assert.Equal(id, stored.Id);
        Assert.Equal("OrderPlaced", stored.Type);
        Assert.Equal("{\"orderId\":42}", stored.Payload);
    }
}
