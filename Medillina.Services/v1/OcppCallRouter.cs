using Medinilla.DataTypes.WAMP;
using Medinilla.Infrastructure;
using Medinilla.Services.Actions;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Medinilla.Services.v1;

public class OcppCallRouter : IOcppCallRouter
{
    private readonly ILogger<OcppCallRouter> _logger;
    private readonly IOcppActionsFactory _actionsFactory;

    public OcppCallRouter(ILogger<OcppCallRouter> logger, IOcppActionsFactory actionsFactory)
    {
        _logger = logger;
        _actionsFactory = actionsFactory;
    }

    public async Task<RpcResult> RouteOcppCall(byte[] buffer, string? clientIdentifier)
    {
        if(clientIdentifier is null)
        {
            throw new Exception("Invalid client identifier");
        }

        var messageString = Encoding.UTF8.GetString(buffer);
        // I'm very ashamed for the way I'm doing this but I was too lazy to write a working Regex, and ChatGPT was failing to give me a working pattern :(
        var parsedMessage = Encoding.UTF8.GetString(buffer).TrimStart('[')
                                                           .TrimEnd(']')
                                                           .Split(',');

        if (int.TryParse(parsedMessage[0].TrimNewLinesAndWhiteSpaces(), out int messageTypeTemp))
        {
            var messageId = parsedMessage[1].ExtractValueInQuotationMarks();

            if (!Enum.GetValues<OcppJMessageType>().Any(c => (int)c == messageTypeTemp))
            {
                return new RpcResult()
                {
                    Error = new OcppCallError(messageId, OcppCallError.ErrorCodes.MessageTypeNotSupported, ""),
                    Result = null
                };
            }

            var messageType = (OcppJMessageType)messageTypeTemp;
            switch(messageType)
            {
                case OcppJMessageType.CALL:
                    var action = parsedMessage[2].ExtractValueInQuotationMarks();
#if DEBUG
                    var salt = new Random().Next().ToString("X");
                    if (!Directory.Exists("logs"))
                    {
                        Directory.CreateDirectory("logs");
                    }
                    if(action != "Heartbeat")
                    {
                        File.WriteAllBytes("logs/" + action + "_log_" + DateTime.Now.ToBinary() + "_" + salt + "_" + ".txt", buffer.ToArray());
                    }
#endif

                    var payload = string.Join(',', parsedMessage.Skip(3));  // TODO: WRITE A FUCKING TOKENIZER!

                    var ocppCall = new OcppCallRequest(messageId, action, payload);

                    var ocppAction = _actionsFactory.GetAction(action);
                    if(ocppAction is null)
                    {
                        _logger.LogError($"Invalid action '{action}'");
                        return new RpcResult()
                        {
                            Error = ocppCall.CreateErrorResult<object>(OcppCallError.ErrorCodes.NotImplemented, $"Action {action} is not implemented on our end."),
                            Result = null,
                            ReturnToCS = true
                        };
                    }

                    return await ocppAction.Execute(ocppCall, clientIdentifier);
                case OcppJMessageType.CALL_RESULT:
                    return new RpcResult()
                    {
                        Result = new OcppCallResult(messageId, parsedMessage[2]),
                        Error = null
                    };
                case OcppJMessageType.CALL_ERROR:
                    var errorCode = parsedMessage[2].ExtractValueInQuotationMarks();
                    var errorDescription = parsedMessage[3].ExtractValueInQuotationMarks();
                    return new RpcResult()
                    {
                        Error = new OcppCallError(messageId, errorCode, errorDescription, parsedMessage[4]),
                        Result = null
                    };
                default:
                    return new RpcResult();
            }
        }
        else
        {
            _logger.LogError("Couldn't parse MessageType from OCPP CALL.");
            return new RpcResult()
            {
                Error = OcppCallError.InternalError,
                Result = null,
                ReturnToCS = true
            };
        }
    }
}
