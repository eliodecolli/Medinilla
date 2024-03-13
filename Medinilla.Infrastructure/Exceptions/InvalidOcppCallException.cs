namespace Medinilla.Infrastructure.Exceptions;

public sealed class InvalidOcppCallException : Exception
{
    private readonly string _fieldName;

    public InvalidOcppCallException(string fieldName)
    {
        _fieldName = fieldName;
    }

    public override string Message => $"Could not parse {_fieldName} from OCPP CALL message."; 
}
