using WebSocket.Manager;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

app.MapGet("/{id}", CustomWebSocketManager.HandleWebSocketRequestAsync);

app.MapPost("/send/{id}", CustomWebSocketManager.SendMessageAsync);

await app.RunAsync();
