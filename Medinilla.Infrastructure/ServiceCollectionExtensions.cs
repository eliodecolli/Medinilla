using Medinilla.Infrastructure.Atomicity;
using Medinilla.Infrastructure.Atomicity.Interfaces;
using Medinilla.Infrastructure.Tokenizer;
using Medinilla.Infrastructure.Tokenizer.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Medinilla.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static void AddMedinillaInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ITokenizer, OcppMessageTokenizer>();
        services.AddScoped<IAtomicProcessor, AtomicProcessor>();
    }
}
