namespace Medinilla.Services.Interfaces;


public interface IWSDigestionServiceCollection
{
    IBasicWebSocketDigestionService? Get(string clientIdentifier);

    void Set(string clientIdentifier, IBasicWebSocketDigestionService service);
}
