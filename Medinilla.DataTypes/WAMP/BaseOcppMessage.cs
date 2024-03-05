namespace Medinilla.DataTypes.WAMP;

public abstract class BaseOcppMessage
{
    public OcppJMessageType MessageType { get; protected set; }

    public string MessageId { get; protected set; }
}
