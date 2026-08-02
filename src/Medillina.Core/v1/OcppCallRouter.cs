using Medinilla.Core.Actions;
using Medinilla.Core.Commands;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Medinilla.Core.v1;

public class OcppCallRouter(
    ILogger<OcppCallRouter> _logger,
    IOcppActionsFactory _factory,
    IOcppChargerCommandFactory _commands,
    IRouterServices services) : IOcppCallRouter
{
    private Func<string, string, CancellationToken, Task>? _submitCall;

    public void SetCallSubmitter(Func<string, string, CancellationToken, Task> submitter)
        => _submitCall = submitter;

    public async Task<RpcResult?> RouteOcppCall(byte[] buffer, string? clientIdentifier)
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
            _logger.LogError(ex, "Error while parsing OCPP message. Payload: {Payload}", messageString);
            return null;
        }

        switch (parser.GetMessageType())
        {
            case OcppJMessageType.CALL:
                return await HandleCall(parser, clientIdentifier, messageString);

            case OcppJMessageType.CALL_RESULT:
            {
                var result = parser.ParseResult();
                _logger.LogInformation("Received OCPP Call Result from {Client} for action '{Action}'", clientIdentifier, ExtractActionFromMessageId(result.MessageId));
                _commands.GetCommand(ExtractActionFromMessageId(result.MessageId) ?? string.Empty)
                         ?.HandleResponse(result.Payload, error: null);
                return null;
            }

            case OcppJMessageType.CALL_ERROR:
            {
                var error = parser.ParseError();
                _logger.LogInformation("Received OCPP Call Error from {Client} for action '{Action}': [{Code}] {Desc}",
                    clientIdentifier, ExtractActionFromMessageId(error.MessageId), error.ErrorCode, error.ErrorDescription);
                _commands.GetCommand(ExtractActionFromMessageId(error.MessageId) ?? string.Empty)
                         ?.HandleResponse(responsePayload: null, error: error);
                return null;
            }

            default:
                return new RpcResult
                {
                    Error = new OcppCallError(parser.TryExtractMessageId() ?? "Unknown", OcppCallError.ErrorCodes.MessageTypeNotSupported, ""),
                    ReturnToCS = true,
                };
        }
    }

    private async Task<RpcResult?> HandleCall(OcppMessageParser parser, string clientIdentifier, string raw)
    {
        var ocppCall = parser.ParseCall();
        _logger.LogInformation("Received OCPP Call: {Action} - from {Client}", ocppCall.Action, clientIdentifier);

#if DEBUG
        var salt = new Random().Next().ToString("X");
        if (!Directory.Exists("logs"))
        {
            Directory.CreateDirectory("logs");
        }
        if (ocppCall.Action != "Heartbeat")
        {
            File.WriteAllBytes("logs/" + ocppCall.Action + "_log_" + DateTime.Now.ToBinary() + "_" + salt + ".txt", Encoding.UTF8.GetBytes(raw));
        }
#endif
        if (!await ValidateRouting(clientIdentifier, ocppCall.Action))
        {
            return new RpcResult
            {
                Result = null,
                Error = OcppCallError.SecurityError(ocppCall.MessageId),
                ReturnToCS = true
            };
        }

        var ocppAction = _factory.GetAction(ocppCall.Action);
        if (ocppAction is null)
        {
            _logger.LogError("Invalid action '{Action}' - Not implemented.", ocppCall.Action);
            return new RpcResult
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
            _logger.LogError(ex, "Error while trying to handle OCPP CALL: Client {Client}", clientIdentifier);
            return new RpcResult
            {
                Error = OcppCallError.InternalError(ocppCall.MessageId),
                Result = null,
                ReturnToCS = true,
            };
        }
    }

    public async Task SubmitAsync(string clientIdentifier, IOcppChargerCommand command, CancellationToken ct)
    {
        if (_submitCall is null)
            throw new InvalidOperationException("Call submitter not wired. Set it from InterfaceCommunication at startup.");

        var messageId = BuildMessageId(command.Action);
        var frame = command.BuildCall(messageId).ToBytes();
        await _submitCall(clientIdentifier, Encoding.UTF8.GetString(frame), ct).ConfigureAwait(false);
    }

    public async Task DisconnectClient(string clientIdentifier)
    {
        await services.DisconnectClient(clientIdentifier);
    }

    private async Task<bool> ValidateRouting(string clientIdentifier, string actionName)
    {
        if (actionName == OcppActionNames.BootNotification)
        {
            return true;
        }

        return await services.ValidateChargingStationAvailability(clientIdentifier);
    }

    private static string BuildMessageId(string action) => $"{action}-{Guid.NewGuid():N}";

    private static string? ExtractActionFromMessageId(string messageId)
    {
        var dash = messageId.IndexOf('-');
        return dash > 0 ? messageId[..dash] : null;
    }
}
