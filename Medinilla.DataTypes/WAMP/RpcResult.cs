namespace Medinilla.DataTypes.WAMP;

public sealed class RpcResult
{
    public OcppCallError? Error { get; set; }

    public OcppCallResult? Result { get; set; }
}
