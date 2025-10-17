namespace Medinilla.Core.SharedContracts.ActorPayloads;

public class WampResultMessage
{
    public string ClientIdentifier {  get; set; }

    public byte[]? Result { get; set; }

    public byte[]? Error { get; set; }

    public bool ReturnToCS { get; set; }
}
