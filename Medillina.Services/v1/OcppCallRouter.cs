using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Medinilla.Services.Actions;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Medinilla.Services.v1;

public class OcppCallRouter : IOcppCallRouter
{
    private readonly ILogger<OcppCallRouter> _logger;
    private readonly IServiceProvider provider;

    public OcppCallRouter(ILogger<OcppCallRouter> logger, IServiceProvider provider)
    {
        _logger = logger;
        this.provider = provider;
    }

    private IOcppActionsFactory GetActionsFactory()
    {
        var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOcppActionsFactory>();
    }

    public async Task<RpcResult> RouteOcppCall(byte[] buffer, string? clientIdentifier)
    {
        ArgumentNullException.ThrowIfNull(clientIdentifier, nameof(clientIdentifier));

        var messageString = Encoding.UTF8.GetString(buffer);

        var parser = new OcppMessageParser();
        parser.LoadRaw(messageString);

        switch (parser.GetMessageType())
        {
            case OcppJMessageType.CALL:
                var ocppCall = parser.ParseCall();
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

                var ocppAction = GetActionsFactory().GetAction(ocppCall.Action);
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
                    Result = parser.ParseResult(),
                    Error = null
                };

            case OcppJMessageType.CALL_ERROR:
                return new RpcResult()
                {
                    Error = parser.ParseError(),
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
