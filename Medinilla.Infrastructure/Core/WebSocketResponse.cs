using Medinilla.Infrastructure.WAMP;

namespace Medinilla.Infrastructure.Core;

public sealed class WebSocketResponse
{
    public WebSocketResponseStatus ResponseStatus { get; set; }

    public OcppCallResult? OcppCallResult { get; set; }

    public OcppCallError? OcppCallError { get; set; }
}
