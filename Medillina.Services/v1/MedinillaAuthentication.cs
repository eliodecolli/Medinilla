using Medinilla.Services.Interfaces;

namespace Medinilla.Services.v1;

public class MedinillaAuthentication : IMedinillaAuthentication
{
    public async Task<string?> ValidateCredentials(string username, string password)
    {
        return await Task.FromResult("TEST-TOKEN");
    }
}
