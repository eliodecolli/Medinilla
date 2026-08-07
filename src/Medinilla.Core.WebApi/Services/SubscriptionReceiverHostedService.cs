using Medinilla.RealTime;
using Medinilla.WebApi.Interfaces;

namespace Medinilla.Core.WebApi.Services;

/// <summary>
/// Boots the per-instance response queue drain with the host. The queue name comes
/// from <see cref="IInstanceIdentifier"/> since IHostedService.StartAsync is parameterless.
/// </summary>
internal sealed class SubscriptionReceiverHostedService(
    ISubscriptionReceiver receiver,
    IInstanceIdentifier instance) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        receiver.Start(instance.ResponseQueue);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
        => receiver.DisposeAsync().AsTask();
}
