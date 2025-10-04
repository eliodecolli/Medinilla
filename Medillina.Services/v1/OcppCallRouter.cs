using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Medinilla.Services.Actions;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Medinilla.Services.v1;

public class OcppCallRouter(ILogger<OcppCallRouter> _logger, IOcppActionsFactory _factory) : IOcppCallRouter
{
    public async Task<RpcResult> RouteOcppCall(byte[] buffer, string? clientIdentifier)
    {
        ArgumentNullException.ThrowIfNull(clientIdentifier, nameof(clientIdentifier));

        var messageString = Encoding.UTF8.GetString(buffer);

        var parser = new OcppMessageParser();
        try
        {
            parser.LoadRaw(messageString);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error while parsing OCPP message: {ex.Message} {Environment.NewLine} Payload: {messageString}");
        }

        switch (parser.GetMessageType())
        {
            case OcppJMessageType.CALL:
                var ocppCall = parser.ParseCall();
                _logger.LogInformation($"Received OCPP Call: {ocppCall.Action} - from {clientIdentifier}");
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

                var ocppAction = _factory.GetAction(ocppCall.Action);
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

                try
                {
                    return await ocppAction.Execute(ocppCall, clientIdentifier);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error while trying to handle OCPP CALL: Client {clientIdentifier}:: {ex.Message}");
                    return new RpcResult()
                    {
                        Error = OcppCallError.InternalError(ocppCall.MessageId),
                        Result = null,
                        ReturnToCS = true,
                    };
                }

            case OcppJMessageType.CALL_RESULT:
                return new RpcResult()
                {
                    Result = parser.ParseResult(),
                    Error = null
                };

            case OcppJMessageType.CALL_ERROR:
                var error = parser.ParseError();

                _logger.LogInformation($"Received OCPP Call Error from {clientIdentifier}: [{error.ErrorCode}]:: {error.ErrorDescription}\n\t-> Details:: {error.ErrorDetails}");

                return new RpcResult()
                {
                    Error = error,
                    Result = null
                };

            default:
                return new RpcResult()
                {
                    Error = new OcppCallError(parser.TryExtractMessageId() ?? "Unknown", OcppCallError.ErrorCodes.MessageTypeNotSupported, ""),
                    Result = null
                };
        }
    }
}
