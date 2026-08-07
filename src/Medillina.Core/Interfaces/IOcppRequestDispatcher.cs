namespace Medinilla.Core.Interfaces;

public interface IOcppRequestDispatcher
{
    Task SubmitRequest(string clientIdentifier, byte[] payload);
}
