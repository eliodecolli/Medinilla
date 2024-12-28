namespace Medinilla.RealTime;

public interface IRealTimeMessenger
{
    Task SendMessage(string channel, byte[] message);

    Task RegisterHandler(string channelName, Func<object, Task> handler);

    Task RegisterChannel(string channelName);

    Task DestroyChannel(string channelName);

    string GetCommunicationProviderName();
}
