using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace FrenchExDev.Net.QualityGate.Cli;

[ExcludeFromCodeCoverage]
internal sealed class WebSocketReloadServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly List<WebSocket> _clients = [];
    private readonly Lock _lock = new();
    private CancellationTokenSource? _cts;

    public WebSocketReloadServer(int port)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/ws/");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener.Start();
        return Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
    }

    public async Task NotifyReloadAsync()
    {
        var payload = Encoding.UTF8.GetBytes("""{"type":"reload"}""");
        var segment = new ArraySegment<byte>(payload);

        List<WebSocket> snapshot;
        lock (_lock)
        {
            snapshot = [.. _clients];
        }

        List<WebSocket> dead = [];

        foreach (var client in snapshot)
        {
            try
            {
                if (client.State == WebSocketState.Open)
                {
                    await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    dead.Add(client);
                }
            }
            catch
            {
                dead.Add(client);
            }
        }

        if (dead.Count > 0)
        {
            RemoveClients(dead);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        lock (_lock)
        {
            foreach (var client in _clients)
            {
                client.Dispose();
            }

            _clients.Clear();
        }

        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        _listener.Close();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(null);
                var socket = wsContext.WebSocket;

                lock (_lock)
                {
                    _clients.Add(socket);
                }

                _ = Task.Run(() => KeepAliveAsync(socket, cancellationToken), cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
        }
    }

    private async Task KeepAliveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[256];

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    break;
                }
            }
        }
        catch
        {
            // Client disconnected unexpectedly.
        }
        finally
        {
            RemoveClients([socket]);
            socket.Dispose();
        }
    }

    private void RemoveClients(List<WebSocket> clients)
    {
        lock (_lock)
        {
            foreach (var client in clients)
            {
                _clients.Remove(client);
            }
        }
    }
}
