namespace Medinilla.Infrastructure.Exceptions;

public sealed class InvalidOcppMessageException(string clientIdentifier) : Exception
{
    public override string Message => $"Invalid OCPP Message from: ${clientIdentifier}";
}
