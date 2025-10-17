namespace Medinilla.Core.SharedContracts.ActorPayloads;

public class OcppConsumerMessage
{
    public string ClientIdentifier { get; set; }

    public byte[] Payload { get; set; }
}
