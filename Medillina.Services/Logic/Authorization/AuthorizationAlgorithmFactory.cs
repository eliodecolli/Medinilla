using Castle.Core.Logging;
using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.Core.Authorization;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.Logic.Authorization;

public sealed class AuthorizationAlgorithmFactory
{
    private Dictionary<AuthorizationAlgorithm, IAuthAlgorithm> _store;
    private ILogger<AuthorizationAlgorithmFactory> logger;

    public AuthorizationAlgorithmFactory(IEnumerable<IAuthAlgorithm> algorithms,
        ILogger<AuthorizationAlgorithmFactory> logger)
    {
        this.logger = logger;
        _store = new Dictionary<AuthorizationAlgorithm, IAuthAlgorithm>();
        foreach (var algorithm in algorithms)
        {
            _store.Add(algorithm.Algorithm, algorithm);
        }
    }

    public IAuthAlgorithm? Get(AuthorizationAlgorithm algorithm) => _store[algorithm] ?? null;

    public IAuthAlgorithm[] All() => [.. _store.Values.OrderBy(a => a.Priority)];

    public async Task<string> RunAuthorization(IdToken? token, AuthorizationContext context)
    {
        var status = AuthorizeStatus.Accepted;

        foreach(var algorithm in All())
        {
            logger.LogInformation($"Running Authorization: {Enum.GetName(algorithm.Algorithm)}");
            status = await algorithm.Authorize(token, context);
            if (status !=  AuthorizeStatus.Accepted)
            {
                break;
            }
        }

        return status;
    }
}
