namespace Medinilla.WebApi.Interfaces;

/// <summary>
/// Identity of the running WebApi process. The response queue every CSMS reply
/// lands on is derived from it, so every WebSocket hosted here shares one queue.
/// </summary>
public interface IInstanceIdentifier
{
    string InstanceId { get; }

    string ResponseQueue { get; }
}
