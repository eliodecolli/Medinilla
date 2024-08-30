namespace Medinilla.Infrastructure.Atomicity.Interfaces;

public delegate void OperationComplete(object? result, IAtomicOperation operation, Exception? ex);

public interface IAtomicProcessor
{
    void EnqueueOperation(IAtomicOperation operation);

    event OperationComplete OnOperationComplete;

    void Start();

    void Stop();
}
