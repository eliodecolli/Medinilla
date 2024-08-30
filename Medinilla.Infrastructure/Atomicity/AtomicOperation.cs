/*
    An atomic operation takes as input a specific Action delegate, and returns a value.
    It is used as a wrapper to implement synchronicity between requests and responses directed to a single CSMS/CS.
 */

using Medinilla.Infrastructure.Atomicity.Interfaces;

namespace Medinilla.Infrastructure.Atomicity;

public delegate Task<object?> Operation(object? state);

public sealed class AtomicOperation : IAtomicOperation
{
    private readonly Operation _operation;
    private readonly object? _state;
    private readonly Guid _id;

    public AtomicOperation(Operation operation, object? state)
        => (_operation, _state, _id) = (operation, state, Guid.NewGuid());

    public async Task<object?> Execute()
    {
        return await _operation(_state);
    }

    public Guid GetOperationId() => _id;
}
