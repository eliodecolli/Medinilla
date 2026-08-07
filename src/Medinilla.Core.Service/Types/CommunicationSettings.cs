using System.Text.Json;

namespace Medinilla.Core.Service.Types;

public sealed class CommunicationSettings
{
    public string RequestQueue { get; private set; }

    private CommunicationSettings(string requestQueue)
    {
        RequestQueue = requestQueue;
    }

    public static CommunicationSettings FromSettingsFile(string settingsFile)
    {
        using var fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read);
        var jsonDocument = JsonDocument.Parse(fs);

        var root = jsonDocument.RootElement;

        // Extract required values from JSON
        var requestQueue = root.GetProperty("RequestQueue").GetString()
            ?? throw new JsonException("RequestQueue is required in settings file");

        return new CommunicationSettings(requestQueue);
    }
}
