using Microsoft.Extensions.Options;
using PubnubApi;

namespace Medinilla.RealTime.PubNub;

public class PubNubClient : IRealTimeMessenger
{
    private readonly Pubnub _pubnub;
    private readonly PubnubConfiguration _config;

    public PubNubClient(IOptions<PubnubConfiguration> config)
    {
        _config = config.Value;
        _pubnub = new Pubnub(new PNConfiguration(new UserId(_config.UserId))
        {
            SubscribeKey = _config.SubscribeKey,
            PublishKey = _config.PublishKey
        });
    }

    public async Task SendMessage(string channel, byte[] message)
    {
        await _pubnub.Publish()
            .Channel(channel)
            .Message(message)
            .ExecuteAsync();
    }
}
