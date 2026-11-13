using Medinilla.DataTypes.Contracts.Common;
using DbIdToken = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;

namespace Medinilla.Core.Interfaces.Services;

public interface IIdTokenService
{
    Task<IdToken> GenerateCentralToken(string clientIdentifier, Guid authUserId, long durationSeconds, string correlationId);
    Task ReleaseCentralToken(DbIdToken token);
}