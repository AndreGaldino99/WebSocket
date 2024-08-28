using System.Net;
using System.Text;

namespace WebSocket.Manager;

using System.Net.WebSockets;

public static class CustomWebSocketManager
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly Dictionary<string, (WebSocket Websocket, string? Group)> _webSocketConnections = [];

    public static async Task HandleWebSocketRequestAsync(HttpContext context)
    {
        string? id = context.Request.RouteValues["id"]?.ToString();
        string? group = context.Request.RouteValues["group"]?.ToString();

        if (string.IsNullOrEmpty(id))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("ID is required.");
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("Request is not a WebSocket request.");
            return;
        }

        (WebSocket Websocket, string? Group) completWebSocket = (await context.WebSockets.AcceptWebSocketAsync(), group);

        await _semaphore.WaitAsync();
        try
        {
            if (_webSocketConnections.TryGetValue(id, out var existingWebSocket))
            {
                if (existingWebSocket.Websocket.State == WebSocketState.Open)
                {
                    existingWebSocket.Websocket.Abort();
                }
                _webSocketConnections.Remove(id);
            }
            _webSocketConnections[id] = completWebSocket!;
        }
        finally
        {
            _semaphore.Release();
        }

        await HandleWebSocketConnectionAsync(id, completWebSocket!);
    }

    public static async Task SendMessageAsync(HttpContext context)
    {
        string? id = context.Request.RouteValues["id"]?.ToString();

        if (string.IsNullOrEmpty(id))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("ID is required.");
            return;
        }

        (WebSocket Websocket, string? Group) webSocket;
        await _semaphore.WaitAsync();
        try
        {
            _webSocketConnections.TryGetValue(id, out webSocket);
        }
        finally
        {
            _semaphore.Release();
        }

        if (webSocket.Websocket == null || webSocket.Websocket.State != WebSocketState.Open)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("WebSocket connection is not open.");
            return;
        }

        using StreamReader reader = new(context.Request.Body);
        string? message = await reader.ReadToEndAsync();

        byte[]? data = Encoding.UTF8.GetBytes(message);

        var websocketConnections = _webSocketConnections.Where(x => x.Value.Group == webSocket.Group);

        foreach (var websocketConnection in websocketConnections)
        {
            await websocketConnection.Value.Websocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        await context.Response.WriteAsync("Message sent via WebSocket.");
    }

    private static async Task HandleWebSocketConnectionAsync(string id, (WebSocket Websocket, string? Group) webSocket)
    {
        byte[]? buffer = new byte[1024 * 4];
        try
        {
            while (webSocket.Websocket.State == WebSocketState.Open)
            {
                var result = await webSocket.Websocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _semaphore.WaitAsync();
                    try
                    {
                        _webSocketConnections.Remove(id);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                    await webSocket.Websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the server", CancellationToken.None);
                }
                else
                {
                    byte[]? data = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    var websocketConnections = _webSocketConnections.Where(x => x.Value.Group == webSocket.Group);

                    foreach (var websocketConnection in websocketConnections)
                    {
                        await websocketConnection.Value.Websocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                    }

                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket connection for ID {id} closed with error: {ex.Message}");
        }
        finally
        {
            await _semaphore.WaitAsync();
            try
            {
                _webSocketConnections.Remove(id);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
