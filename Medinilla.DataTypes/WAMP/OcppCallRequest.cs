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

    public byte[] ToByteArray()
    {
        var payload = string.Compare(Payload, "null") == 0 ? "{}" : Payload;
        var requestString = $"[{MessageType},\"{MessageId}\",\"{Action}\",{payload}]";
#if DEBUG
        Console.WriteLine("-------------{0}OCPP CALL Request: {1}{0}{2}{0}-------------", Environment.NewLine, requestString, payload);
#endif
        return Encoding.UTF8.GetBytes(requestString);
    }

    public T As<T>() where T : class
    {
        var result = JsonSerializer.Deserialize<T>(Payload, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
        if(result is not null)
        {
            return result;
        }

        throw new Exception($"Couldn't deserialize Payload to type {typeof(T).Name}");
    }

    public OcppCallResult CreateResult<T>(T payload)
        where T : class
    {
        return new OcppCallResult(MessageId, JsonSerializer.Serialize(payload, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    public OcppCallError CreateErrorResult<T>(string errorCode, string errorDescription = "", T? details = null)
        where T: class
    {
        return new OcppCallError(MessageId, errorCode, errorDescription, JsonSerializer.Serialize(details));
    }
}