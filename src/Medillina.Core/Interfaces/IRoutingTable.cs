namespace Medinilla.Core.Interfaces;

internal interface IRoutingTable<T>
{
    public Task<T?> TryGetValue(string messageId);

    public Task Remove(string messageId);

    public Task Add(string messageId, T value);
}
