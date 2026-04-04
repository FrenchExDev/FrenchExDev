using FrenchExDev.Net.Mediator.Testing;

namespace FrenchExDev.Net.Mediator.Tests;

// -- Test fixtures ----------------------------------------------------------

public sealed record GetUserQuery(int UserId) : IQuery<string>;

public sealed record CreateUserCommand(string Name) : ICommand<int>;

public sealed record UserCreatedNotification(string Name) : INotification;

public sealed record OrderPlacedNotification(int OrderId) : INotification;

// -- Tests ------------------------------------------------------------------

public sealed class FakeMediatorTests
{
    private readonly FakeMediator _sut = new();

    [Fact]
    public async Task SendAsync_WithSetup_ReturnsCannedResponse()
    {
        // Arrange
        _sut.Setup<GetUserQuery, string>(q => $"User-{q.UserId}");

        // Act
        var result = await _sut.SendAsync(new GetUserQuery(42));

        // Assert
        Assert.Equal("User-42", result);
    }

    [Fact]
    public async Task SendAsync_RecordsRequest()
    {
        // Arrange
        _sut.Setup<GetUserQuery, string>(_ => "ignored");

        // Act
        await _sut.SendAsync(new GetUserQuery(7));

        // Assert
        var recorded = Assert.Single(_sut.SentRequests);
        var query = Assert.IsType<GetUserQuery>(recorded);
        Assert.Equal(7, query.UserId);
    }

    [Fact]
    public async Task SendAsync_WithoutSetup_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SendAsync(new GetUserQuery(1)));

        Assert.Contains("GetUserQuery", ex.Message);
    }

    [Fact]
    public async Task PublishAsync_RecordsNotification()
    {
        // Act
        await _sut.PublishAsync(new UserCreatedNotification("Alice"));

        // Assert
        var recorded = Assert.Single(_sut.PublishedNotifications);
        var notification = Assert.IsType<UserCreatedNotification>(recorded);
        Assert.Equal("Alice", notification.Name);
    }

    [Fact]
    public async Task WasSent_ReturnsTrueWhenRequestWasSent()
    {
        // Arrange
        _sut.Setup<GetUserQuery, string>(_ => "x");
        await _sut.SendAsync(new GetUserQuery(1));

        // Act & Assert
        Assert.True(_sut.WasSent<GetUserQuery>());
        Assert.False(_sut.WasSent<CreateUserCommand>());
    }

    [Fact]
    public async Task WasPublished_ReturnsTrueWhenNotificationWasPublished()
    {
        // Arrange
        await _sut.PublishAsync(new UserCreatedNotification("Bob"));

        // Act & Assert
        Assert.True(_sut.WasPublished<UserCreatedNotification>());
        Assert.False(_sut.WasPublished<OrderPlacedNotification>());
    }

    [Fact]
    public async Task Reset_ClearsEverything()
    {
        // Arrange
        _sut.Setup<GetUserQuery, string>(_ => "x");
        await _sut.SendAsync(new GetUserQuery(1));
        await _sut.PublishAsync(new UserCreatedNotification("C"));

        // Act
        _sut.Reset();

        // Assert
        Assert.Empty(_sut.SentRequests);
        Assert.Empty(_sut.PublishedNotifications);

        // Setup was cleared too, so sending should throw
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SendAsync(new GetUserQuery(2)));
    }

    [Fact]
    public async Task MultipleSends_RecordedInOrder()
    {
        // Arrange
        _sut.Setup<GetUserQuery, string>(q => $"U{q.UserId}");
        _sut.Setup<CreateUserCommand, int>(c => c.Name.Length);

        // Act
        await _sut.SendAsync(new GetUserQuery(1));
        await _sut.SendAsync(new CreateUserCommand("Alice"));
        await _sut.SendAsync(new GetUserQuery(2));

        // Assert
        Assert.Equal(3, _sut.SentRequests.Count);
        Assert.IsType<GetUserQuery>(_sut.SentRequests[0]);
        Assert.IsType<CreateUserCommand>(_sut.SentRequests[1]);
        Assert.IsType<GetUserQuery>(_sut.SentRequests[2]);
    }

    [Fact]
    public async Task MultiplePublishes_RecordedInOrder()
    {
        // Act
        await _sut.PublishAsync(new UserCreatedNotification("A"));
        await _sut.PublishAsync(new OrderPlacedNotification(100));
        await _sut.PublishAsync(new UserCreatedNotification("B"));

        // Assert
        Assert.Equal(3, _sut.PublishedNotifications.Count);
        Assert.IsType<UserCreatedNotification>(_sut.PublishedNotifications[0]);
        Assert.IsType<OrderPlacedNotification>(_sut.PublishedNotifications[1]);
        Assert.IsType<UserCreatedNotification>(_sut.PublishedNotifications[2]);
    }

    [Fact]
    public async Task SendAsync_WithCommandSetup_ReturnsCannedResponse()
    {
        // Arrange
        _sut.Setup<CreateUserCommand, int>(cmd => cmd.Name.Length);

        // Act
        var result = await _sut.SendAsync(new CreateUserCommand("Bob"));

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task PublishAsync_WithStrategy_StillRecords()
    {
        // Act
        await _sut.PublishAsync(new UserCreatedNotification("X"), strategy: PublishStrategy.Parallel);
        await _sut.PublishAsync(new OrderPlacedNotification(1), strategy: PublishStrategy.FireAndForget);

        // Assert
        Assert.Equal(2, _sut.PublishedNotifications.Count);
    }
}
