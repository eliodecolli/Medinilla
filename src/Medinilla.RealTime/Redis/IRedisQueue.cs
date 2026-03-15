namespace Medinilla.RealTime.Redis;

public interface IRedisQueue : IDisposable
{
    Task<byte[]?> WaitForMessage(string channel, CancellationToken ct = default);
    Task SendMessage(byte[] message, string channel);
}
