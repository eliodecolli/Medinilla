namespace Medinilla.Core.Logic.Exceptions;

public sealed class TransactionException : Exception
{
    public TransactionException(string message) : base(message)
    {
    }
}
