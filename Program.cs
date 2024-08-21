using System.Net;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

var webSocketConnections = new Dictionary<string, WebSocket>();
var connectionLock = new object();

app.MapGet("/{id}", async context =>
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
    lock (connectionLock)
    {
        if (webSocketConnections.TryGetValue(id, out var existingWebSocket))
        {
            if (existingWebSocket.State == WebSocketState.Open)
            {
                existingWebSocket.Abort();
            }
            webSocketConnections.Remove(id);
        }
        webSocketConnections[id] = webSocket;
    }

    await HandleWebSocketConnectionAsync(id, webSocket);
});

app.MapPost("/send/{id}", async context =>
{
    var id = context.Request.RouteValues["id"]?.ToString();
    if (string.IsNullOrEmpty(id))
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await context.Response.WriteAsync("ID is required.");
        return;
    }

    WebSocket? webSocket;
    lock (connectionLock)
    {
        webSocketConnections.TryGetValue(id, out webSocket);
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
});

await app.RunAsync();

async Task HandleWebSocketConnectionAsync(string id, WebSocket webSocket)
{
    var buffer = new byte[1024 * 4];
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                lock (connectionLock)
                {
                    webSocketConnections.Remove(id);
                }
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the server", CancellationToken.None);
            }
            else
            {
                // Optionally process received data here
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WebSocket connection for ID {id} closed with error: {ex.Message}");
    }
    finally
    {
        lock (connectionLock)
        {
            webSocketConnections.Remove(id);
        }
    }
}
