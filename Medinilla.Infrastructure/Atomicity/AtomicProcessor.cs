using Medinilla.Infrastructure.Atomicity.Interfaces;
using System.Collections.Concurrent;

namespace Medinilla.Infrastructure.Atomicity;

public class AtomicProcessor : IAtomicProcessor
{
    private readonly ConcurrentQueue<IAtomicOperation> _operations;
    private bool _running;

    public AtomicProcessor()
    {
        _operations = new ConcurrentQueue<IAtomicOperation>();
        _running = false;
    }

    public event OperationComplete OnOperationComplete;

    public void EnqueueOperation(IAtomicOperation operation)
    {
        _operations.Enqueue(operation);

#if DEBUG
        Console.WriteLine("[x] Enqueued operation. Total operations: {0}", _operations.Count);
#endif
    }

    private void CallEvent(object? state, IAtomicOperation operation, Exception? ex)
    {
        if (OnOperationComplete != null)
        {
            OnOperationComplete(state, operation, ex);
        }
    }

    public void Start()
    {
#if DEBUG
        Console.WriteLine("[x] Starting Atomic Processor");
#endif
        _running = true;
        Task.Run(async () => { 
            while(_running)
            {
                if(!_operations.IsEmpty)
                {
                    if(_operations.TryDequeue(out var operation))
                    {
#if DEBUG
                        Console.WriteLine("[x] Got one operation awaiting.");
#endif
                        try
                        {
                            var result = await operation.Execute();
#if DEBUG
                            Console.WriteLine("[x] Finished exectuing 1 operation.");
#endif
                            CallEvent(result, operation, null);
                        }
                        catch(Exception ex)
                        {
                            CallEvent(null, operation, ex);
                        }
                    }
                }
            }
        });
    }

    public void Stop()
    {
        _running = false;
    }
}
