namespace Medinilla.Services.Interfaces;

public interface IMedinillaAuthentication
{
    public Task<string?> ValidateCredentials(string username, string password);
}
