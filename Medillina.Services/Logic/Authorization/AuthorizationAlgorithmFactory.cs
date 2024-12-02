using Medinilla.Core.Interfaces;
using Medinilla.DataTypes.Core.Authorization;

namespace Medinilla.Core.Logic.Authorization;

public sealed class AuthorizationAlgorithmFactory
{
    private Dictionary<AuthorizationAlgorithm, IAuthAlgorithm> _store;

    public AuthorizationAlgorithmFactory(IEnumerable<IAuthAlgorithm> algorithms)
    {
        _store = new Dictionary<AuthorizationAlgorithm, IAuthAlgorithm>();
        foreach (var algorithm in algorithms)
        {
            _store.Add(algorithm.Algorithm, algorithm);
        }
    }

    public IAuthAlgorithm? Get(AuthorizationAlgorithm algorithm) => _store[algorithm] ?? null;

    public IAuthAlgorithm[] All() => [.. _store.Values];
}
