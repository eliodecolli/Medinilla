using Medinilla.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Medinilla.WebApi.Controllers
{
    [Route("api")]
    [ApiController]
    public class WSController : ControllerBase
    {
        private readonly IBasicWebSocketDigestionService _webSocketDigestionService;
        
        public WSController(IBasicWebSocketDigestionService webSocketDigestionService)
        {
            _webSocketDigestionService = webSocketDigestionService;
        }

        [HttpGet("/ws/{clientIdentifier}")]
        public async Task Get(string? clientIdentifier)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext()
                {
                    DangerousEnableCompression = true,
                    SubProtocol = "ocpp2.0.1"
                });
                
                Console.WriteLine("Accepting connection from client {0}", clientIdentifier);
                await _webSocketDigestionService.Consume(webSocket, clientIdentifier);
            }
        }
    }
}
