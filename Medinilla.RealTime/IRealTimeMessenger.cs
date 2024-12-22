namespace Medinilla.RealTime;

public interface IRealTimeMessenger
{
    Task SendMessage(string channel, byte[] message);
}
