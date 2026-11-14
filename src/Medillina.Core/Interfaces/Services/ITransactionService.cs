using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.Interfaces.Services;

/// <summary>
/// Internal transaction ingestion outcome. <see cref="Response"/> is the OCPP response
/// payload; <see cref="WasNewlyIngested"/> is false for replayed sequence numbers and
/// authorization rejections that did not append a transaction event.
/// </summary>
public readonly record struct TransactionIngestionResult(
    TransactionEventResponse Response,
    bool WasNewlyIngested);

public interface ITransactionService
{
    Task<TransactionIngestionResult> IngestEvent(string clientIdentifier, TransactionEventRequest request);
}
