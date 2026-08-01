namespace Medinilla.RealTime;

public interface IReceiver : IDisposable
{
    Task<byte[]?> ReceiveAsync(string queue, CancellationToken ct = default);
}
