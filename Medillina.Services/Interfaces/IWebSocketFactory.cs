namespace Medinilla.Services.Interfaces;

public interface IWebSocketFactory
{
    public IBasicWebSocketDigestionService? GetWebSocketDigestionService(string clientIdentifier);

    public void RegisterWebSocketDigestionService(string clientIdentifier, IBasicWebSocketDigestionService service);
}
