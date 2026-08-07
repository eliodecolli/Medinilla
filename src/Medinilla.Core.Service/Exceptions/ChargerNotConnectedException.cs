namespace Medinilla.Core.Service.Exceptions;

/// <summary>
/// Thrown when a CSMS-initiated call targets a charger that no WebApi instance is
/// currently hosting — there is no response queue to push the CALL onto.
/// </summary>
public sealed class ChargerNotConnectedException(string clientIdentifier)
    : Exception($"Charger '{clientIdentifier}' is not connected to any instance.")
{
    public string ClientIdentifier { get; } = clientIdentifier;
}
