using Microsoft.AspNetCore.Mvc;
using Websocket.Service.Interfaces;

namespace Websocket.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WebsocketManagerController(IWebsocketManagerService _websocketManagerService) : Controller
{
    [HttpGet("{group}/{id}")]
    public async Task HandleWebSocketRequestAsync(string group, string id)
    {
        await _websocketManagerService.HandleWebSocketRequestAsync(HttpContext, group, id);
    }

    [HttpPost("send/{groupId}")]
    public async Task<IActionResult> SendMessageAsync(string groupId, int messageType)
    {
        await _websocketManagerService.SendMessageAsync(HttpContext, groupId, messageType);
        return Ok();
    }
}
