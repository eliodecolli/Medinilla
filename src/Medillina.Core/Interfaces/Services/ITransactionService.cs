using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.Interfaces.Services;

public interface ITransactionService
{
    Task<TransactionEventResponse> IngestEvent(string clientIdentifier, TransactionEventRequest request);
}