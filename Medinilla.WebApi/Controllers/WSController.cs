using Medinilla.DataTypes.WAMP;
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

        private readonly IOcppMessageParser _parser;
        
        public WSController(IBasicWebSocketDigestionService webSocketDigestionService, IWSDigestionServiceCollection _collection, IOcppMessageParser parser)
        {
            _webSocketDigestionService = webSocketDigestionService;
            _wsDigestionServiceCollection = _collection;
            _parser = parser;
        }

        private async Task WriteHttpResponse(string response, int code)
        {
            HttpContext.Response.StatusCode = code;
            await HttpContext.Response.WriteAsync(response);
        }

        [Consumes(MediaTypeNames.Text.Plain)]
        [HttpPost("/ws/{clientIdentifier}")]
        public async Task Post(string? clientIdentifier, [FromBody]string data)
        {
            var service = _wsDigestionServiceCollection.Get(clientIdentifier ?? "");
            if (service is not null)
            {
                _parser.LoadRaw(data);
                if (_parser.GetMessageType() != OcppJMessageType.CALL)
                {
                    await WriteHttpResponse("Invalid OCPP Message: Only CALL types are supported for this operation.", StatusCodes.Status400BadRequest);
                }
                else
                {
                    await service.Send(_parser.ParseCall());
                }
            }
            else
            {
                await WriteHttpResponse("Could not load service required to perform operation. Please check the provided client identifier or make sure the charging station is connected to the server",
                    StatusCodes.Status400BadRequest);
            }
        }

        [HttpGet("/ws/{clientIdentifier}")]
        public async Task Get(string? clientIdentifier)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                if (string.IsNullOrEmpty(clientIdentifier))
                {
                    await WriteHttpResponse("Client Identifier must be provided.", StatusCodes.Status400BadRequest);
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
                await WriteHttpResponse("Only websocket connections are allowed.", StatusCodes.Status400BadRequest);
                return;
            }
        }
    }
}
