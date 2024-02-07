using Medinilla.Services.v1;
using Medinilla.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Medinilla.Services;

public static class ServiceExtensions
{
    public static void AddMedinillaServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IMedinillaAuthentication, MedinillaAuthentication>();
    }
}
