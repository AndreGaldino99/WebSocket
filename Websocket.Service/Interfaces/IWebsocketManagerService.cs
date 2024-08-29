using Microsoft.AspNetCore.Http;

namespace Websocket.Service.Interfaces;

public interface IWebsocketManagerService
{
    Task HandleWebSocketRequestAsync(HttpContext context, string? id, string? group);
    Task SendMessageAsync(HttpContext context, string? id);
}
