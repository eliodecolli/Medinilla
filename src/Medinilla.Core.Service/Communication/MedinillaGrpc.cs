using Grpc.Core;
using Medinilla.Core.gRPC.Contracts;
using Medinilla.Core.gRPC.Service;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Service.Communication.Mapping;
using Medinilla.Core.Service.Exceptions;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Medinilla.Core.Service.Communication;

internal sealed class MedinillaGrpc(ILogger<MedinillaGrpc> log, IServiceProvider serviceProvider) : OcppService.OcppServiceBase
{
    /// <summary>
    /// A charger that no instance is hosting can't be called at all, so the request
    /// fails as an OCPP CALLERROR rather than being queued for nobody.
    /// </summary>
    private Error NotConnectedError(ChargerNotConnectedException ex, string actionName)
    {
        log.LogWarning("{ci}: {an}: charger is not connected", ex.ClientIdentifier, actionName);

        var callError = new OcppCallError(
            "-1",
            OcppCallError.ErrorCodes.GenericError,
            $"NotConnected: {ex.Message}");

        return new Error
        {
            HasError = true,
            Message = Encoding.UTF8.GetString(callError.ToByteArray()),
        };
    }

    public override async Task<SetVariablesResponse> SetVariables(SetVariablesRequest request, ServerCallContext context)
    {
        try
        {
            var messageId = Guid.NewGuid().ToString();
            log.LogInformation("Request: {an} msgId={mi} ci={ci}",
                OcppActionNames.SetVariables,
                messageId,
                request.ClientIdentifier);

            var payload = MedinillaMapping.MapSetVariables(request);

            var ocppRequest = new OcppCallRequest(messageId, OcppActionNames.SetVariables, OcppPayloadSerializer.SerializePayload(payload));
            using var scope = serviceProvider.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();

            await router.SubmitAsync(request.ClientIdentifier, ocppRequest);
            return new SetVariablesResponse()
            {
                Error = new Error()
                {
                    HasError = false,
                }
            };
        }
        catch (ChargerNotConnectedException e)
        {
            return new SetVariablesResponse()
            {
                Error = NotConnectedError(e, OcppActionNames.SetVariables)
            };
        }
        catch (Exception e)
        {
            log.LogError("{ci}: {an}: Error: {msg}", request.ClientIdentifier, OcppActionNames.SetVariables, e.Message);
            return new SetVariablesResponse()
            {
                Error = new Error()
                {
                    HasError = true,
                    Message = e.Message,
                }
            };
        }
    }

    public override async Task<GetVariablesResponse> GetVariables(GetVariablesRequest request, ServerCallContext context)
    {
        try
        {
            var messageId = Guid.NewGuid().ToString();
            log.LogInformation("Request: {an} msgId={mi} ci={ci}",
                OcppActionNames.GetVariables,
                messageId,
                request.ClientIdentifier);

            var payload = MedinillaMapping.MapGetVariables(request);

            var ocppRequest = new OcppCallRequest(messageId, OcppActionNames.GetVariables, OcppPayloadSerializer.SerializePayload(payload));
            using var scope = serviceProvider.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();

            await router.SubmitAsync(request.ClientIdentifier, ocppRequest);
            return new GetVariablesResponse()
            {
                Error = new Error()
                {
                    HasError = false,
                }
            };
        }
        catch (ChargerNotConnectedException e)
        {
            return new GetVariablesResponse()
            {
                Error = NotConnectedError(e, OcppActionNames.GetVariables)
            };
        }
        catch (Exception e)
        {
            log.LogError("{ci}: {an}: Error: {msg}", request.ClientIdentifier, OcppActionNames.GetVariables, e.Message);
            return new GetVariablesResponse()
            {
                Error = new Error()
                {
                    HasError = true,
                    Message = e.Message,
                }
            };
        }
    }

    public override async Task<GetBaseReportResponse> GetBaseReport(GetBaseReportRequest request, ServerCallContext context)
    {
        try
        {
            var messageId = Guid.NewGuid().ToString();
            log.LogInformation("Request: {an} msgId={mi} ci={ci}",
                OcppActionNames.GetBaseReport,
                messageId,
                request.ClientIdentifier);

            var payload = MedinillaMapping.MapGetBaseReport(request);

            var ocppRequest = new OcppCallRequest(messageId, OcppActionNames.GetBaseReport, OcppPayloadSerializer.SerializePayload(payload));
            using var scope = serviceProvider.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();

            await router.SubmitAsync(request.ClientIdentifier, ocppRequest);
            return new GetBaseReportResponse()
            {
                Error = new Error()
                {
                    HasError = false,
                }
            };
        }
        catch (ChargerNotConnectedException e)
        {
            return new GetBaseReportResponse()
            {
                Error = NotConnectedError(e, OcppActionNames.GetBaseReport)
            };
        }
        catch (Exception e)
        {
            log.LogError("{ci}: {an}: Error: {msg}", request.ClientIdentifier, OcppActionNames.GetBaseReport, e.Message);
            return new GetBaseReportResponse()
            {
                Error = new Error()
                {
                    HasError = true,
                    Message = e.Message,
                }
            };
        }
    }

    public override async Task<FetchExecutedCommandsResponse> FetchExecutedCommands(FetchExecutedCommandsRequest request, ServerCallContext context)
    {
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICommandExecutionService>();

        var result = await service.FetchExecutionsForCharger(request.ClientIdentifier);
        var retval = new FetchExecutedCommandsResponse();
        retval.Executions.AddRange(result.Select(r => new CommandExecution()
        {
            ActionName = r.ActionName,
            ClientIdentifier = request.ClientIdentifier,
            Error = r.Error,
            ErrorMessage = r.ErrorMessage ?? ""
        }));

        return retval;
    }
}
