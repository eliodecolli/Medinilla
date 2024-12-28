using Medinilla.WebApi.Interfaces;

namespace Medinilla.WebApi;

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

    public void Remove(string clientIdentifier)
    {
        if (_table.TryGetValue(clientIdentifier, out var val))
        {
            _table.Remove(clientIdentifier);
        }
    }

    public void Set(string clientIdentifier, IBasicWebSocketDigestionService service)
    {
        _table[clientIdentifier] = service;
    }
}
