namespace Medinilla.Infrastructure.Atomicity.Interfaces;

public interface IAtomicOperation
{
    Task<object?> Execute();

    Guid GetOperationId();
}
