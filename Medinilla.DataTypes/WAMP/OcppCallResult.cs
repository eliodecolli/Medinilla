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
        var payload = string.Compare(Payload, "null") == 0 ? "{}" : Payload;

        var responseString = $"[{(int)MessageType},\"{MessageId}\",{payload}]";
        return Encoding.UTF8.GetBytes(responseString);
    }
}
