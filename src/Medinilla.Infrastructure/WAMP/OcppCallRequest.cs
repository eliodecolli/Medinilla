using System.Text.Json;
using System.Text.Json.Serialization;

namespace Medinilla.Infrastructure.WAMP;

public sealed class OcppCallRequest : BaseOcppMessage
{
    public OcppCallRequest(string msgId, string action, string payload)
    {
        MessageType = OcppJMessageType.CALL;
        MessageId = msgId;
        Action = action;
        Payload = payload;
    }

    public string Action { get; private set; }

    public string Payload { get; private set; }

    public T As<T>() where T : class
    {
        var result = JsonSerializer.Deserialize<T>(Payload, new JsonSerializerOptions()
        { 
            PropertyNameCaseInsensitive = true,
            Converters = { new DottedEnumJsonConverter() }
        });
        if (result is not null)
        {
            return result;
        }

        throw new Exception($"Couldn't deserialize Payload to type {typeof(T).Name}");
    }

    public OcppCallResult CreateResult<T>(T payload)
        where T : class
    {
        return new OcppCallResult(MessageId, OcppPayloadSerializer.SerializePayload(payload));
    }

    public string Serialize()
    {
        var payload = string.Compare(Payload, "null") == 0 ? "{}" : Payload;

        var responseString = $"[{(int)MessageType},\"{MessageId}\",\"{Action}\",{payload}]";
        return responseString;
    }

    public OcppCallError CreateErrorResult<T>(string errorCode, string errorDescription = "", T? details = null)
        where T : class
    {
        return new OcppCallError(MessageId, errorCode, errorDescription, JsonSerializer.Serialize(details));
    }
}