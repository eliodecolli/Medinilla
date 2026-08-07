using Medinilla.Core.Actions;
using Medinilla.Core.Commands;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Medinilla.Core.v1;

public class OcppCallRouter(
    ILogger<OcppCallRouter> _logger,
    IRouterServices services,
    IOcppActionsFactory actionsFactory,
    IOcppChargerCommandFactory commandFactory,
    IOcppRequestDispatcher dispatcher,
    BaseOcppRoutingTable outboundTable) : IOcppCallRouter
{
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
                {
                    var call = parser.ParseCall();
                    return await HandleCall(clientIdentifier, call).ConfigureAwait(false);
                }

            case OcppJMessageType.CALL_RESULT:
                {
                    var result = parser.ParseResult();
                    await DispatchCommandReplyAsync(clientIdentifier, result, cmd => cmd.HandleResponse(result))
                        .ConfigureAwait(false);

                    return null;
                }

            case OcppJMessageType.CALL_ERROR:
            {
                var error = parser.ParseError();
                await DispatchCommandReplyAsync(clientIdentifier, error, cmd => cmd.HandleError(error))
                        .ConfigureAwait(false);

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

    private async Task<RpcResult?> HandleCall(string clientIdentifier, OcppCallRequest ocppCall)
    {
        _logger.LogInformation("Received OCPP Call: {Action} - from {Client}", ocppCall.Action, clientIdentifier);

        var ocppAction = actionsFactory.GetAction(ocppCall.Action);
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
        
        if (!await ValidateRouting(clientIdentifier, ocppCall.Action))
        {
            _logger.LogError("Could not validate message from {clientIdentifier}: {action}", clientIdentifier,  ocppCall.Action);
            return new RpcResult
            {
                Result = null,
                Error = OcppCallError.SecurityError(ocppCall.MessageId),
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

    private async Task DispatchCommandReplyAsync<TMessage>(
        string clientIdentifier,
        TMessage message,
        Func<IOcppChargerCommand, Task> dispatch)
        where TMessage : BaseOcppMessage
    {

        var pending = await outboundTable.TryGetValue(message.MessageId).ConfigureAwait(false);
        if (pending is not null)
        {
            var command = commandFactory.GetCommand(pending);
            if (command is null)
            {
                _logger.LogError("Message {mid} is marked as {cmd}, but {cmd} is not implemented in our end.", message.MessageId, pending, pending);
                return;
            }

            await outboundTable.Remove(message.MessageId);
            await dispatch(command);

            _logger.LogInformation("Response: action={action} msgId={msgId} ci={ci}",
                pending,
                message.MessageId,
                clientIdentifier);
        }
        else
        {
            _logger.LogError("Received message reply, however in-flight message was not present in our table. Message ID: {msgId}, Client ID: {clientId}", message.MessageId, clientIdentifier);
        }
    }

    public async Task SubmitAsync(string clientIdentifier, OcppCallRequest request)
    {
        await outboundTable.Add(request.MessageId, request.Action).ConfigureAwait(false);

        var payload = Encoding.UTF8.GetBytes(request.Serialize());

        try
        {
            await dispatcher.SubmitRequest(clientIdentifier, payload).ConfigureAwait(false);
        }
        catch
        {
            // Dispatch never happened, so nothing will ever reply — don't leave the
            // message sitting in the in-flight table.
            await outboundTable.Remove(request.MessageId).ConfigureAwait(false);
            throw;
        }
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
}
