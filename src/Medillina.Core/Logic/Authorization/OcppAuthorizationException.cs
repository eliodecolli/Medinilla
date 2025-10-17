namespace Medinilla.Core.Logic.Authorization;

public sealed class OcppAuthorizationException(string message) : Exception(message)
{
}
