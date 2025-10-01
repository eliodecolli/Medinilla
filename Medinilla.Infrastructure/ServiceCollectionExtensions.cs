using Medinilla.Infrastructure.Interfaces;
using Medinilla.Infrastructure.Tokenizer;
using Medinilla.Infrastructure.Tokenizer.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Medinilla.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static void AddMedinillaInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ITokenizer, OcppMessageTokenizerJson>();
        services.AddScoped<IOcppMessageParser, OcppMessageParser>();
    }
}
