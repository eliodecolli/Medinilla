using System.Text.Json;

namespace Medinilla.Core.Service.Types;

public sealed class CommunicationSettings
{
    public string RabbitUri { get; private set; }

    public string SignalChannel { get; private set; }

    private CommunicationSettings(string rabbitUri, string signalChannel)
    {
        RabbitUri = rabbitUri;
        SignalChannel = signalChannel;
    }

    public static CommunicationSettings FromSettingsFile(string settingsFile)
    {
        using var fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read);
        var jsonDocument = JsonDocument.Parse(fs);

        var root = jsonDocument.RootElement;

        // Extract required values from JSON
        var rabbitUri = root.GetProperty("RabbitUri").GetString()
            ?? throw new JsonException("RabbitUri is required in settings file");

        var signalChannel = root.GetProperty("SignalChannel").GetString()
            ?? throw new JsonException("SignalChannel is required in settings file");

        return new CommunicationSettings(rabbitUri, signalChannel);
    }
}
