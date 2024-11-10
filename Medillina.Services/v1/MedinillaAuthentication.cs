using Medinilla.DataAccess.Interfaces;
using Medinilla.Services.Interfaces;

namespace Medinilla.Services.v1;

public class MedinillaAuthentication(IFastAccessDataSource fastAccessDataSource) : IMedinillaAuthentication
{
    public async Task<string?> ValidateCredentials(string base64EncodedCredentials)
    {
        var token = fastAccessDataSource.GetValue<string>(base64EncodedCredentials);
        if(!string.IsNullOrEmpty(token))
        {
            return await Task.FromResult(token);
        }

        fastAccessDataSource.SetValue(base64EncodedCredentials, "TEST-TOKEN-FASTACCESS");
        return await Task.FromResult("TEST-TOKEN");
    }
}
