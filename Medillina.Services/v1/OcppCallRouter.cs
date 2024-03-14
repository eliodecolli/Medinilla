using Medinilla.DataTypes.WAMP;
using Medinilla.Services.Actions;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Medinilla.Services.v1;

public class OcppCallRouter : IOcppCallRouter
{
    private readonly ILogger<OcppCallRouter> _logger;
    private readonly IOcppActionsFactory _actionsFactory;
    private readonly IOcppMessageParser _parser;

    public OcppCallRouter(ILogger<OcppCallRouter> logger, IOcppActionsFactory actionsFactory, IOcppMessageParser parser)
    {
        _logger = logger;
        _actionsFactory = actionsFactory;
        _parser = parser;
    }

    public async Task<RpcResult> RouteOcppCall(byte[] buffer, string? clientIdentifier)
    {
        if (clientIdentifier is null)
        {
            throw new Exception("Invalid client identifier");
        }

        var messageString = Encoding.UTF8.GetString(buffer);
        _parser.LoadRaw(messageString);

        switch (_parser.GetMessageType())
        {
            case OcppJMessageType.CALL:
                var ocppCall = _parser.ParseCall();
#if DEBUG
                var salt = new Random().Next().ToString("X");
                if (!Directory.Exists("logs"))
                {
                    Directory.CreateDirectory("logs");
                }
                if (ocppCall.Action != "Heartbeat")
                {
                    File.WriteAllBytes("logs/" + ocppCall.Action + "_log_" + DateTime.Now.ToBinary() + "_" + salt + "_" + ".txt", buffer.ToArray());
                }
#endif

                var ocppAction = _actionsFactory.GetAction(ocppCall.Action);
                if (ocppAction is null)
                {
                    _logger.LogError($"Invalid action '{ocppCall.Action}'");
                    return new RpcResult()
                    {
                        Error = ocppCall.CreateErrorResult<object>(OcppCallError.ErrorCodes.NotImplemented, $"Action {ocppCall.Action} is not implemented on our end."),
                        Result = null,
                        ReturnToCS = true
                    };
                }

                return await ocppAction.Execute(ocppCall, clientIdentifier);

            case OcppJMessageType.CALL_RESULT:
                return new RpcResult()
                {
                    Result = _parser.ParseResult(),
                    Error = null
                };

            case OcppJMessageType.CALL_ERROR:
                return new RpcResult()
                {
                    Error = _parser.ParseError(),
                    Result = null
                };

            default:
                return new RpcResult()
                {
                    Error = new OcppCallError(_parser.TryExtractMessageId() ?? "Unknown", OcppCallError.ErrorCodes.MessageTypeNotSupported, ""),
                    Result = null
                };
        }
    }
}
