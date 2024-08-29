using Microsoft.AspNetCore.Mvc;
using Websocket.Service.Interfaces;

namespace Websocket.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WebsocketManagerController(IWebsocketManagerService _websocketManagerService) : Controller
{
    [HttpGet("{group}/{id}")]
    public async Task HandleWebSocketRequestAsync([FromRoute]string group, [FromRoute]string id, [FromQuery]string? geminiKey)
    {
        await _websocketManagerService.HandleWebSocketRequestAsync(HttpContext, group, id, geminiKey);
    }

    [HttpPost("send/{id}")]
    public async Task<IActionResult> SendMessageAsync(string id)
    {
        await _websocketManagerService.SendMessageAsync(HttpContext, id);
        return Ok();
    }
}
