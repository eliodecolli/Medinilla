using Medinilla.Services.Interfaces;
using Medinilla.WebApi.ApiModels;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace Medinilla.WebApi.Controllers
{
    [Route("api")]
    [ApiController]
    public class WSController : ControllerBase
    {
        private readonly IBasicWebSocketDigestionService _webSocketDigestionService;

        private readonly IWSDigestionServiceCollection _wsDigestionServiceCollection;
        
        public WSController(IBasicWebSocketDigestionService webSocketDigestionService, IWSDigestionServiceCollection _collection)
        {
            _webSocketDigestionService = webSocketDigestionService;
            _wsDigestionServiceCollection = _collection;
        }

        [Consumes(MediaTypeNames.Text.Plain)]
        [HttpPost("/ws/{clientIdentifier}")]
        public async Task Post(string? clientIdentifier, [FromBody]string data)
        {
            var service = _wsDigestionServiceCollection.Get(clientIdentifier ?? "");
            if (service is not null)
            {
                await service.Send(data);
            }
        }

        [HttpGet("/ws/{clientIdentifier}")]
        public async Task Get(string? clientIdentifier)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                if (string.IsNullOrEmpty(clientIdentifier))
                {
                    HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await HttpContext.Response.WriteAsync("Client Identifier must be provided.");
                    return;
                }

                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext()
                {
                    DangerousEnableCompression = true,
                    SubProtocol = "ocpp2.0.1"
                });
                
                Console.WriteLine("Accepting connection from client {0}", clientIdentifier);
                _wsDigestionServiceCollection.Set(clientIdentifier, _webSocketDigestionService);

                await _webSocketDigestionService.Consume(webSocket, clientIdentifier);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await HttpContext.Response.WriteAsync("Only websocket connections are allowed.");
                return;
            }
        }
    }
}
