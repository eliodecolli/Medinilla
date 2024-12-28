using Medinilla.DataAccess.Interfaces;
using Medinilla.Services.Interfaces;

namespace Medinilla.Services.v1;

public class MedinillaAuthentication : IMedinillaAuthentication
{
    private IFastAccessDataSource _fastAccessDataSource;

    public MedinillaAuthentication(IFastAccessDataSource fastAccessDataSource)
    {
        _fastAccessDataSource = fastAccessDataSource;
    }

    public async Task<string?> ValidateCredentials(string base64EncodedCredentials)
    {
        var token = _fastAccessDataSource.GetValue<string>(base64EncodedCredentials);
        if (!string.IsNullOrEmpty(token))
        {
            return await Task.FromResult(token);
        }

        _fastAccessDataSource.SetValue(base64EncodedCredentials, "TEST-TOKEN-FASTACCESS");
        return await Task.FromResult("TEST-TOKEN");
    }
}
