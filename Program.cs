using System.Net;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseWebSockets();


List<(System.Net.WebSockets.WebSocket,string)> ListWebSocketConnection = [];
app.MapGet("/{id}", async context =>
{
    var id = context.Request.RouteValues["id"]?.ToString();
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
    else
    {
        var itemRemove = ListWebSocketConnection.Where(x => x.Item2 == id).FirstOrDefault();
        ListWebSocketConnection.Remove(itemRemove);
        ListWebSocketConnection.Add((await context.WebSockets.AcceptWebSocketAsync(), id)!);

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
});

app.MapPost("/send/{id}", async context =>
{
    var id = context.Request.RouteValues["id"]?.ToString();
    var webSocketConnection = ListWebSocketConnection.Where(x=>x.Item2 == id).FirstOrDefault().Item1;
    if (webSocketConnection == null || webSocketConnection.State != WebSocketState.Open)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await context.Response.WriteAsync("WebSocket connection is not open.");
        return;
    }

    using var reader = new StreamReader(context.Request.Body);
    var message = await reader.ReadToEndAsync();

    var data = Encoding.ASCII.GetBytes(message);
    await webSocketConnection.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);

    context.Response.StatusCode = (int)HttpStatusCode.OK;
    await context.Response.WriteAsync("Message sent via WebSocket.");
});

await app.RunAsync();
