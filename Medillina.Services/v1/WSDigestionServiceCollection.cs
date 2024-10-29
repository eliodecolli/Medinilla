using Medinilla.Services.Interfaces;

namespace Medinilla.Services.v1;

public class WSDigestionServiceCollection : IWSDigestionServiceCollection
{
    private readonly Dictionary<string, IBasicWebSocketDigestionService> _table = new Dictionary<string, IBasicWebSocketDigestionService>();

    public IBasicWebSocketDigestionService? Get(string clientIdentifier)
    {
        if(_table.TryGetValue(clientIdentifier, out var val))
        {
            return val;
        }

        return null;
    }

    public void Set(string clientIdentifier, IBasicWebSocketDigestionService service)
    {
        _table[clientIdentifier] = service;
    }
}
