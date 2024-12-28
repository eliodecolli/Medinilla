using Medinilla.WebApi.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class WSController : ControllerBase
{
    private readonly IBasicWebSocketDigestionService webSocketDigestionService;
    private readonly IWSDigestionServiceCollection _wsDigestionServiceCollection;
    private readonly ILogger<WSController> _logger;

    public WSController(
        IBasicWebSocketDigestionService webSocketDigestionService,
        IWSDigestionServiceCollection wsDigestionServiceCollection,
        ILogger<WSController> logger)
    {
        this.webSocketDigestionService = webSocketDigestionService;
        _wsDigestionServiceCollection = wsDigestionServiceCollection;
        _logger = logger;
    }

    [HttpGet("/ws/{clientIdentifier}")]
    public async Task Get(string? clientIdentifier)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            await WriteHttpResponse("Only websocket connections are allowed.", StatusCodes.Status400BadRequest);
            return;
        }

        if (string.IsNullOrEmpty(clientIdentifier))
        {
            await WriteHttpResponse("Client Identifier must be provided.", StatusCodes.Status400BadRequest);
            return;
        }

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync(
            new WebSocketAcceptContext
            {
                DangerousEnableCompression = true,
                SubProtocol = "ocpp2.0.1"
            });

        _logger.LogInformation("Accepting connection from client {ClientId}", clientIdentifier);

        try
        {
            _wsDigestionServiceCollection.Set(clientIdentifier, webSocketDigestionService);
            await webSocketDigestionService.Consume(webSocket, clientIdentifier);
        }
        finally
        {
            _wsDigestionServiceCollection.Remove(clientIdentifier);
        }
    }

    private async Task WriteHttpResponse(string response, int code)
    {
        HttpContext.Response.StatusCode = code;
        await HttpContext.Response.WriteAsync(response);
    }
}