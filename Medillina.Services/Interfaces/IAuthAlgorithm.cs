using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Core.Logic.Authorization;
using Medinilla.Infrastructure.Core.Authorization;

namespace Medinilla.Core.Interfaces;

public interface IAuthAlgorithm
{
    int Priority { get; }

    AuthorizationAlgorithm Algorithm { get; }

    Task<string> Authorize(IdToken? idToken,
        AuthorizationContext context);
}
