namespace Medinilla.Core.Events;

/// <summary>
/// Non-wire outcome attached to a TransactionEvent CALL result so the publication
/// boundary can distinguish a newly ingested event from a replayed sequence number.
/// </summary>
public sealed record TransactionEventProcessingContext(bool WasNewlyProcessed);
