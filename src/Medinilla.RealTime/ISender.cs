namespace Medinilla.RealTime;

public interface ISender : IDisposable
{
    Task SendAsync(string queue, byte[] message, CancellationToken ct = default);
}
