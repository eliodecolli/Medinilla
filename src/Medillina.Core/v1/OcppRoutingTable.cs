using Medinilla.Core.Interfaces;

namespace Medinilla.Core.v1;

public abstract class BaseOcppRoutingTable : IRoutingTable<string>
{
    public abstract Task Add(string messageId, string value);

    public abstract Task Remove(string messageId);

    public abstract Task<string?> TryGetValue(string messageId);
}
