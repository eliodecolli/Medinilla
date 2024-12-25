namespace Medinilla.RealTime;

public interface ICommunicationProvider
{
    IRealTimeMessenger? GetMessenger(string providerName);
}