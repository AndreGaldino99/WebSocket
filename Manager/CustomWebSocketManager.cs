using System.Net;
using System.Text;

namespace WebSocket.Manager;

using System.Net.WebSockets;

public static class CustomWebSocketManager
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly Dictionary<string, WebSocket> _webSocketConnections = [];

    public static async Task HandleWebSocketRequestAsync(HttpContext context)
    {
        var id = context.Request.RouteValues["id"]?.ToString();
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

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await _semaphore.WaitAsync();  
        try
        {
            if (_webSocketConnections.TryGetValue(id, out var existingWebSocket))
            {
                if (existingWebSocket.State == WebSocketState.Open)
                {
                    existingWebSocket.Abort();
                }
                _webSocketConnections.Remove(id);
            }
            _webSocketConnections[id] = webSocket;
        }
        finally
        {
            _semaphore.Release();  
        }

        await HandleWebSocketConnectionAsync(id, webSocket);
    }

    public static async Task SendMessageAsync(HttpContext context)
    {
        var id = context.Request.RouteValues["id"]?.ToString();
        if (string.IsNullOrEmpty(id))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("ID is required.");
            return;
        }

        WebSocket? webSocket;
        await _semaphore.WaitAsync();  
        try
        {
            _webSocketConnections.TryGetValue(id, out webSocket);
        }
        finally
        {
            _semaphore.Release();  
        }

        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await context.Response.WriteAsync("WebSocket connection is not open.");
            return;
        }

        using var reader = new StreamReader(context.Request.Body);
        var message = await reader.ReadToEndAsync();

        var data = Encoding.UTF8.GetBytes(message);
        
        await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        await context.Response.WriteAsync("Message sent via WebSocket.");
    }

    private static async Task HandleWebSocketConnectionAsync(string id, WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
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
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the server", CancellationToken.None);
                }
                else
                {
                    
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
