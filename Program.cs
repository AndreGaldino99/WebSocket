using WebSocket.Manager;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

app.MapGet("/{id}", async context =>
{
    await WebSocket.Manager.WebSocketManager.HandleWebSocketRequestAsync(context);
});

app.MapPost("/send/{id}", async context =>
{
    await WebSocket.Manager.WebSocketManager.SendMessageAsync(context);
});

await app.RunAsync();
