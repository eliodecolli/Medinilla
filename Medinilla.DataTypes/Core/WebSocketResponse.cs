using Medinilla.DataTypes.WAMP;

namespace Medinilla.DataTypes.Core;

public sealed class WebSocketResponse
{
    public WebSocketResponseStatus ResponseStatus { get; set;  }

    public OcppCallResult? OcppCallResult { get; set; }

    public OcppCallError? OcppCallError { get; set; }
}
