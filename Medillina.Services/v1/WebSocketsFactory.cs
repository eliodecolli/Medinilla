using Medinilla.Services.Interfaces;

namespace Medinilla.Services.v1;

public sealed class WebSocketsFactory : IWebSocketFactory
{
    private readonly Dictionary<string, IBasicWebSocketDigestionService> _wsRegistry;

    public WebSocketsFactory()
    {
        _wsRegistry = new Dictionary<string, IBasicWebSocketDigestionService>();
    }

    public IBasicWebSocketDigestionService? GetWebSocketDigestionService(string clientIdentifier)
    {
        if(_wsRegistry.TryGetValue(clientIdentifier, out var service))
        {
            return service;
        }

        return null;
    }

    public void RegisterWebSocketDigestionService(string clientIdentifier, IBasicWebSocketDigestionService service)
    {
        _wsRegistry.Add(clientIdentifier, service);
    }
}
