using Medinilla.Core.Interfaces;
using Medinilla.Core.Service.Types;
using Medinilla.RealTime;
using Medinilla.RealTime.Redis;
using System.Text;

namespace Medinilla.Core.Service.Communication;

internal sealed class OcppRequestDispatcher(ISender sender, CommunicationSettings settings) : IOcppRequestDispatcher
{
    public async Task SubmitRequest(string clientIdentifier, string payload)
    {
        var channelName = RedisUtils.BuildChannelName(settings.ResponseQueue, clientIdentifier);
        await sender.SendAsync(channelName, Encoding.UTF8.GetBytes(payload));
    }
}
