namespace Medinilla.RealTime;

public class CommunicationProvider : ICommunicationProvider
{
    private readonly IEnumerable<IRealTimeMessenger> _messengers;

    public CommunicationProvider(IEnumerable<IRealTimeMessenger> messengers)
    {
        _messengers = messengers;
    }

    public IRealTimeMessenger? GetMessenger(string providerName)
    {
        return _messengers.FirstOrDefault(m => m.GetCommunicationProviderName() == providerName);
    }
}
