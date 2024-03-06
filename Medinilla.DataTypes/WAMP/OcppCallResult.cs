using System.Text;
using System.Text.Json;

namespace Medinilla.DataTypes.WAMP;

public sealed class OcppCallResult : BaseOcppMessage
{
    public OcppCallResult(string msgId, string? payload)
    {
        MessageType = OcppJMessageType.CALL_RESULT;
        MessageId = msgId;
        Payload = payload;
    }

    public string? Payload { get; private set; }

    public T? PayloadAs<T>() where T : class
    {
        if(Payload is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(Payload);
    }

    public byte[] ToByteArray()
    {
        var payload = Payload is not null ? JsonSerializer.Serialize(Payload) : "{}";
        return Encoding.UTF8.GetBytes($"[{(int)MessageType},\"{MessageId}\",{payload}]");
    }
}
