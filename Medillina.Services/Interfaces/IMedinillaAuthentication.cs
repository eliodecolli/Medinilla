namespace Medinilla.Services.Interfaces;

public interface IMedinillaAuthentication
{
    public Task<string?> ValidateCredentials(string base64EncodedCredentials);
}
