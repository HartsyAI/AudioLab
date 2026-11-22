using System.Net.WebSockets;
using System.Text;

namespace AudioLab.Server.Tests;

/// <summary>A hand-written SignalR client that can stop reading, which the real client cannot: it joins a project and then lets the server's messages pile up.</summary>
public sealed class RawHubSocket : IAsyncDisposable
{
    private const char Separator = '\u001e';

    private readonly ClientWebSocket _socket = new();

    public WebSocketState State => _socket.State;

    public static async Task<RawHubSocket> JoinAsync(RunningServer server, string project)
    {
        RawHubSocket client = new();
        UriBuilder address = new(new Uri(server.Client.BaseAddress!, server.HubPath)) { Scheme = "ws" };
        await client._socket.ConnectAsync(address.Uri, CancellationToken.None);
        await client.SendAsync("{\"protocol\":\"json\",\"version\":1}");
        await client.ReadMessageAsync();
        await client.SendAsync("{\"type\":1,\"invocationId\":\"1\",\"target\":\"Join\",\"arguments\":[\"" + project + "\"]}");
        string reply = await client.ReadMessageAsync();
        if (!reply.Contains("\"type\":3", StringComparison.Ordinal) || reply.Contains("\"error\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The raw client could not join.");
        }
        return client;
    }

    /// <summary>Whether the server has closed the connection, which it does when it aborts it.</summary>
    public async Task<bool> ServerClosedAsync(TimeSpan within)
    {
        using CancellationTokenSource timeout = new(within);
        byte[] buffer = new byte[64 * 1024];
        try
        {
            while (true)
            {
                WebSocketReceiveResult result = await _socket.ReceiveAsync(buffer, timeout.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return true;
                }
            }
        }
        catch (WebSocketException)
        {
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _socket.Dispose();
        await Task.CompletedTask;
    }

    private Task SendAsync(string json)
    {
        return _socket.SendAsync(Encoding.UTF8.GetBytes(json + Separator), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task<string> ReadMessageAsync()
    {
        StringBuilder text = new();
        byte[] buffer = new byte[64 * 1024];
        while (true)
        {
            WebSocketReceiveResult result = await _socket.ReceiveAsync(buffer, CancellationToken.None);
            text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (text.Length > 0 && text[^1] == Separator)
            {
                return text.ToString(0, text.Length - 1);
            }
        }
    }
}
