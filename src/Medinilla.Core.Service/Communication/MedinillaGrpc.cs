using Grpc.Core;
using Medinilla.Core.gRPC.Contracts;
using Medinilla.Core.gRPC.Service;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.v1.Exceptions;
using Medinilla.DataAccess.Exceptions;
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
            await SubmitAsync(request.ClientIdentifier, ocppRequest);
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
            await SubmitAsync(request.ClientIdentifier, ocppRequest);
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
            await SubmitAsync(request.ClientIdentifier, ocppRequest);
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

    public override async Task<ResetResponse> Reset(ResetRequest request, ServerCallContext context)
    {
        try
        {
            var messageId = Guid.NewGuid().ToString();
            log.LogInformation("Request: {an} msgId={mi} ci={ci}",
                OcppActionNames.Reset,
                messageId,
                request.ClientIdentifier);

            var payload = MedinillaMapping.MapReset(request);

            var ocppRequest = new OcppCallRequest(messageId, OcppActionNames.Reset, OcppPayloadSerializer.SerializePayload(payload));
            await SubmitAsync(request.ClientIdentifier, ocppRequest);
            return new ResetResponse()
            {
                Error = new Error()
                {
                    HasError = false,
                }
            };
        }
        catch (ChargerNotConnectedException e)
        {
            return new ResetResponse()
            {
                Error = NotConnectedError(e, OcppActionNames.Reset)
            };
        }
        catch (Exception e)
        {
            log.LogError("{ci}: {an}: Error: {msg}", request.ClientIdentifier, OcppActionNames.Reset, e.Message);
            return new ResetResponse()
            {
                Error = new Error()
                {
                    HasError = true,
                    Message = e.Message,
                }
            };
        }
    }

    private static RequestStartTransactionResponse StartTransactionError(string correlationId, string message) => new()
    {
        CorrelationId = correlationId,
        Error = new Error
        {
            HasError = true,
            Message = message,
        },
    };

    public override async Task<RequestStartTransactionResponse> RequestStartTransaction(RequestStartTransactionRequest request, ServerCallContext context)
    {
        var messageId = Guid.NewGuid().ToString();
        try
        {
            if (string.IsNullOrWhiteSpace(request.ClientIdentifier))
                return StartTransactionError(messageId, "client_identifier is required.");

            if (!Guid.TryParse(request.AuthorizationUserId, out var authUserId) || authUserId == Guid.Empty)
                return StartTransactionError(messageId, "authorization_user_id must be a valid, non-empty GUID.");

            if (request.DurationSeconds <= 0
                || request.DurationSeconds > (long)(DateTime.MaxValue - DateTime.UtcNow).TotalSeconds)
                return StartTransactionError(messageId, "duration_seconds must be positive and produce a valid expiry date.");

            log.LogInformation("Request: {an} msgId={mi} ci={ci}",
                OcppActionNames.RequestStartTransaction,
                messageId,
                request.ClientIdentifier);

            using var scope = serviceProvider.CreateScope();
            var tokenService = scope.ServiceProvider.GetRequiredService<IIdTokenService>();
            var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();

            var payload = MedinillaMapping.MapRequestStartTransaction(request);
            payload.IdToken = await tokenService.GenerateCentralToken(
                request.ClientIdentifier,
                authUserId,
                request.DurationSeconds,
                messageId);

            var ocppRequest = new OcppCallRequest(messageId, OcppActionNames.RequestStartTransaction, OcppPayloadSerializer.SerializePayload(payload));
            await SubmitAsync(request.ClientIdentifier, ocppRequest, router);
            return new RequestStartTransactionResponse()
            {
                Token = new IdToken()
                {
                    Token = payload.IdToken.Token,
                    Type = payload.IdToken.Type.ToString(),
                    AdditionalInfo = payload.IdToken.AdditionalInfo is not null ? new AdditionalTokenInfo()
                    {
                        AdditionalIdToken = payload.IdToken.AdditionalInfo.AdditionalIdToken,
                        Type = payload.IdToken.AdditionalInfo.Type
                    } : null
                },
                CorrelationId = messageId,
                Error = new Error()
                {
                    HasError = false,
                }
            };
        }
        catch (AuthorizationUserNotFoundException e)
        {
            log.LogWarning(e, "{ci}: {an}: Authorization user {uid} not found msgId={mi}",
                request.ClientIdentifier, OcppActionNames.RequestStartTransaction, request.AuthorizationUserId, messageId);
            return StartTransactionError(messageId, "Authorization user not found for this charging station.");
        }
        catch (AggregateRootNotFoundException e)
        {
            log.LogWarning(e, "{ci}: {an}: Charging station not found msgId={mi}",
                request.ClientIdentifier, OcppActionNames.RequestStartTransaction, messageId);
            return StartTransactionError(messageId, "Charging station not found.");
        }
        catch (ChargerNotConnectedException e)
        {
            return new RequestStartTransactionResponse()
            {
                CorrelationId = messageId,
                Error = NotConnectedError(e, OcppActionNames.RequestStartTransaction)
            };
        }
        catch (Exception e)
        {
            log.LogError(e, "{ci}: {an}: Failed to start transaction msgId={mi}",
                request.ClientIdentifier, OcppActionNames.RequestStartTransaction, messageId);
            return StartTransactionError(messageId, "Unable to start transaction due to an internal error.");
        }
    }

    public override async Task<RequestStopTransactionResponse> RequestStopTransaction(RequestStopTransactionRequest request, ServerCallContext context)
    {
        try
        {
            var messageId = Guid.NewGuid().ToString();
            log.LogInformation("Request: {an} msgId={mi} ci={ci}",
                OcppActionNames.RequestStopTransaction,
                messageId,
                request.ClientIdentifier);

            var payload = MedinillaMapping.MapRequestStopTransaction(request);

            var ocppRequest = new OcppCallRequest(messageId, OcppActionNames.RequestStopTransaction, OcppPayloadSerializer.SerializePayload(payload));
            await SubmitAsync(request.ClientIdentifier, ocppRequest);
            return new RequestStopTransactionResponse()
            {
                Error = new Error()
                {
                    HasError = false,
                }
            };
        }
        catch (ChargerNotConnectedException e)
        {
            return new RequestStopTransactionResponse()
            {
                Error = NotConnectedError(e, OcppActionNames.RequestStopTransaction)
            };
        }
        catch (Exception e)
        {
            log.LogError("{ci}: {an}: Error: {msg}", request.ClientIdentifier, OcppActionNames.RequestStopTransaction, e.Message);
            return new RequestStopTransactionResponse()
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

    private async Task SubmitAsync(string clientIdentifier, OcppCallRequest request, IOcppCallRouter router)
    {
        await router.SubmitAsync(clientIdentifier, request);
    }
    
    private async Task SubmitAsync(string clientIdentifier, OcppCallRequest request)
    {
        using var scope = serviceProvider.CreateScope();
        var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();
        await SubmitAsync(clientIdentifier, request, router);
    }
    
}
