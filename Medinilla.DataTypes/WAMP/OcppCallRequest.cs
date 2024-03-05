using Medinilla.Infrastructure;
using System.Text;
using System.Text.Json;

namespace Medinilla.DataTypes.WAMP;

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
        var result = JsonSerializer.Deserialize<T>(Payload);
        if(result is not null)
        {
            return result;
        }

        throw new Exception($"Couldn't deserialize Payload to type {typeof(T).Name}");
    }

    public OcppCallResult CreateResult<T>(string payload)
    {
        return new OcppCallResult(MessageId, payload);
    }

    public OcppCallError CreateErrorResult(string errorCode, string errorDescription = "", string? details = null)
    {
        return new OcppCallError(MessageId, errorCode, errorDescription, details);
    }
}