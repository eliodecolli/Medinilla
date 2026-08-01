namespace Medinilla.RealTime;

public interface IMessageQueue : IDisposable
{
    Task<byte[]?> ReceiveAsync(string queue, CancellationToken ct = default);

    Task SendAsync(byte[] message, string queue, CancellationToken ct = default);
}
