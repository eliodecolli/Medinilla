using Medinilla.DataAccess.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Medinilla.DataAccess;

public static class ServiceExtensions
{
    public static void AddMedinillaDataSources(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IFastAccessDataSource, InMemoryFastAccessDataSource>();
    }
}
