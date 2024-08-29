using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Websocket.Arguments;
using Websocket.Service.Interfaces;

namespace Websocket.Service.Services;

public class WebsocketManagerService : IWebsocketManagerService
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly Dictionary<string, (WebSocket Websocket, string? Group)> _webSocketConnections = [];

    public async Task HandleWebSocketRequestAsync(HttpContext context, string? group, string? id, string? geminiKey)
    {
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

        await HandleWebSocketConnectionAsync(id, completWebSocket!, geminiKey);
    }

    private static async Task HandleWebSocketConnectionAsync(string id, (WebSocket Websocket, string? Group) webSocket, string? geminiKey)
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
                    byte[]? data = Encoding.UTF8.GetBytes(await SendGeminiRequest(Encoding.UTF8.GetString(buffer, 0, result.Count), geminiKey));

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

    public async Task SendMessageAsync(HttpContext context, string? id)
    {
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

    public static async Task<string> SendGeminiRequest(string message, string? geminiKey)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={geminiKey}";

        using HttpClient client = new();
        var jsonContent = JsonConvert.SerializeObject(new GeminiRequest([new([new PartRequest(message)])]));

        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        try
        {
            HttpResponseMessage response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var res = JsonConvert.DeserializeObject<GeminiResponse>(await response.Content.ReadAsStringAsync());
                return res?.Candidates?[0]?.Content?.Parts?[0]?.Text ?? "";
            }
            else
            {
                return ("Erro: " + response.StatusCode);
            }
        }
        catch (HttpRequestException e)
        {
            return ("Erro na requisição: " + e.Message);
        }
    }
}
