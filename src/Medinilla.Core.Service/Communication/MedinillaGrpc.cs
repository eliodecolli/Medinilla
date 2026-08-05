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

namespace Medinilla.Core.Service.Communication;

internal sealed class MedinillaGrpc(ILogger<MedinillaGrpc> log, IServiceProvider serviceProvider) : OcppService.OcppServiceBase
{
    public override async Task<SetVariablesResponse> SetVariables(SetVariablesRequest request, ServerCallContext context)
    {
        log.LogInformation("{ci}: {an}", request.ClientIdentifier, OcppActionNames.SetVariables);

        try
        {
            var messageId = new Guid().ToString();
            var payload = MedinillaMapping.MapSetVariables(request);

            var ocppRequest = new OcppCallRequest(messageId, OcppActionNames.SetVariables, JsonSerializer.Serialize(payload));
            using var scope = serviceProvider.CreateScope();
            var router = scope.ServiceProvider.GetRequiredService<IOcppCallRouter>();

            await router.SubmitAsync(request.ClientIdentifier, ocppRequest);
            return new SetVariablesResponse()
            {
                Error = new Error()
                {
                    HasError = false,
                    Message = null,
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
}
