using RabbitMQ.Client;
namespace Medinilla.Core.Service.Communication;

public sealed class ChannelSemaphore : IAsyncDisposable
{
    private readonly IChannel channel;
    private readonly SemaphoreSlim semaphore;
    private bool disposed;

    public ChannelSemaphore(IChannel channel)
    {
        this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
        semaphore = new SemaphoreSlim(1, 1);
    }

    public async Task<IChannel> GetChannelAsync()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ChannelSemaphore));

        await semaphore.WaitAsync();
        return channel;
    }

    public void ReleaseChannel()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ChannelSemaphore));

        semaphore.Release();
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        disposed = true;
        semaphore.Dispose();

        await channel.DisposeAsync();
    }
}