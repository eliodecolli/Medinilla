using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Medinilla.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using Medinilla.Core.Actions;
using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Core;

namespace Medinilla.Core.v1;

public class OcppCallRouter(ILogger<OcppCallRouter> _logger, IOcppActionsFactory _factory, IRouterServices services) : IOcppCallRouter
{
    private async Task<bool> ValidateRouting(string clientIdentifier, string actionName)
    {
        if (actionName == OcppActionNames.BootNotification)
        {
            return true;
        }

        return await services.ValidateChargingStationAvailability(clientIdentifier);
    }
    
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
                if (!await ValidateRouting(clientIdentifier, ocppCall.Action))
                {
                    return new RpcResult()
                    {
                        Result = null,
                        Error = OcppCallError.SecurityError(ocppCall.MessageId),
                        ReturnToCS = true
                    };
                }
                
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

    public async Task DisconnectClient(string clientIdentifier)
    {
        await services.DisconnectClient(clientIdentifier);
    }
}
