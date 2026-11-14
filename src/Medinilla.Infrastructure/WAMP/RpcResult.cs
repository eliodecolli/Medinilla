namespace Medinilla.Infrastructure.WAMP;

public sealed class RpcResult
{
    public OcppCallError? Error { get; set; }

    public OcppCallResult? Result { get; set; }

    public bool ReturnToCS { get; set; } = false;

    /// <summary>
    /// Optional non-wire context produced by an action for Core completion hooks.
    /// It is never serialized into the OCPP response.
    /// </summary>
    public object? ProcessingContext { get; set; }
}
