using FrenchExDev.Net.Outbox;

using Xunit;

namespace FrenchExDev.Net.Outbox.Tests;

public sealed class OutboxMessageTests
{
    [Fact]
    public void New_message_has_non_empty_id()
    {
        var message = new OutboxMessage();
        Assert.NotEqual(Guid.Empty, message.Id);
    }

    [Fact]
    public void New_message_has_empty_type()
    {
        var message = new OutboxMessage();
        Assert.Equal(string.Empty, message.Type);
    }

    [Fact]
    public void New_message_has_empty_payload()
    {
        var message = new OutboxMessage();
        Assert.Equal(string.Empty, message.Payload);
    }

    [Fact]
    public void New_message_has_recent_created_at()
    {
        var before = DateTimeOffset.UtcNow;
        var message = new OutboxMessage();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(message.CreatedAt, before, after);
    }

    [Fact]
    public void New_message_has_null_processed_at()
    {
        var message = new OutboxMessage();
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public void New_message_has_zero_attempts()
    {
        var message = new OutboxMessage();
        Assert.Equal(0, message.Attempts);
    }

    [Fact]
    public void New_message_has_null_last_error()
    {
        var message = new OutboxMessage();
        Assert.Null(message.LastError);
    }

    [Fact]
    public void Two_messages_have_different_ids()
    {
        var message1 = new OutboxMessage();
        var message2 = new OutboxMessage();
        Assert.NotEqual(message1.Id, message2.Id);
    }
}
