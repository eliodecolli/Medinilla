namespace Medinilla.DataTypes.Pubnub;

public enum PubnubMessageHeader : int
{
    Set,
    Remove
}

public class PubnubMessage<T>
{
    public PubnubMessageHeader Header { get; set; }

    public T Payload { get; set; }
}
