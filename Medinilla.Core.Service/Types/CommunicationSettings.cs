using System.Text.Json;

namespace Medinilla.Core.Service.Types;

public sealed class CommunicationSettings
{
    public string RabbitUri { get; private set; }

    public string RequestQueue { get; private set; }

    public string ResponseQueue { get; private set; }

    private CommunicationSettings(string rabbitUri, string requestQueue, string responseQueue)
    {
        RabbitUri = rabbitUri;
        RequestQueue = requestQueue;
        ResponseQueue =  responseQueue;
    }

    public static CommunicationSettings FromSettingsFile(string settingsFile)
    {
        using var fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read);
        var jsonDocument = JsonDocument.Parse(fs);

        var root = jsonDocument.RootElement;

        // Extract required values from JSON
        var rabbitUri = root.GetProperty("RabbitUri").GetString()
            ?? throw new JsonException("RabbitUri is required in settings file");

        var requestQueue = root.GetProperty("RequestQueue").GetString()
            ?? throw new JsonException("RequestQueue is required in settings file");

        var responseQueue = root.GetProperty("ResponseQueue").GetString()
            ?? throw new JsonException("ResponseQueue is required in settings file");

        return new CommunicationSettings(rabbitUri, requestQueue, responseQueue);
    }
}
