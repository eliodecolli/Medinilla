using Grpc.Core;
using Medinilla.Core.gRPC.Service;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Communication.Mapping;
using Medinilla.Core.Service.Types;
using Medinilla.Core.v1;
using Medinilla.Infrastructure.WAMP;
using Medinilla.RealTime;
using Medinilla.RealTime.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Medinilla.Core.Service.Communication;

internal sealed class MedinillaGrpc(ILogger<MedinillaGrpc> log, IServiceProvider serviceProvider) : OcppService.OcppServiceBase
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string SerializePayload<T>(T payload) => JsonSerializer.Serialize(payload, PayloadJsonOptions);

    public override async Task<SetVariablesResponse> SetVariables(SetVariablesRequest request, ServerCallContext context)
    {
        try
        {
            var messageId = Guid.NewGuid().ToString();
            log.LogInformation("{ci}: {an} - {mi}",
                request.ClientIdentifier,
                OcppActionNames.SetVariables,
                messageId);

            var payload = MedinillaMapping.MapSetVariables(request);

            var ocppRequest = new OcppCallRequest(messageId, OcppActionNames.SetVariables, SerializePayload(payload));
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
            log.LogInformation("{ci}: {an} - {mi}",
                request.ClientIdentifier,
                OcppActionNames.GetVariables,
                messageId);

            var payload = MedinillaMapping.MapGetVariables(request);

            var ocppRequest = new OcppCallRequest(messageId, OcppActionNames.GetVariables, SerializePayload(payload));
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
}
