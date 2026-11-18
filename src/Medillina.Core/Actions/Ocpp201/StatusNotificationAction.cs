using Medinilla.Core.Interfaces.Services;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataTypes.Contracts;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Actions.Ocpp201;

public sealed class StatusNotificationAction(
    IStatusNotificationService service,
    ILogger<StatusNotificationAction> logger) : IOcppAction
{
    public string ActionName => OcppActionNames.StatusNotification;

    public async Task<RpcResult> Execute(OcppCallRequest call, string clientIdentifier)
    {
        var request = call.As<StatusNotificationRequest>();

        try
        {
            await service.ProcessNotification(clientIdentifier, request);

            return new RpcResult()
            {
                Result = call.CreateResult(new StatusNotificationResponse()),
                ReturnToCS = true
            };
        }
        catch (AggregateRootNotFoundException)
        {
            logger.LogError(
                $"Something weird has happened: Charging Station with ID {clientIdentifier} does not exist on our end.");
            return new RpcResult()
            {
                Error = call.CreateErrorResult<StatusNotificationResponse>(OcppCallError.ErrorCodes.GenericError,
                    $"Client Identifier {clientIdentifier} not found."),
                ReturnToCS = true
            };
        }
    }
}
