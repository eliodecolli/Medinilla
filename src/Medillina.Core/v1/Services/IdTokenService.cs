using System.Security.Claims;
using System.Text;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Configuration;
using Medinilla.Core.v1.Exceptions;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational;
using Medinilla.DataTypes.Contracts.Common;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using DbToken = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;
using DbIdTokenType = Medinilla.DataAccess.Relational.Enums.IdTokenType;

namespace Medinilla.Core.v1.Services;

public class IdTokenService(
    ILogger<IdTokenService> logger,
    MedinillaOcppDbContext context
    ) : IIdTokenService
{
    private static IdToken MapToContractToken(DbToken token)
    {
        return new IdToken
        {
            Token = token.Token,
            Type = Enum.Parse<IdTokenType>(token.IdType.ToString()),
        };
    }

    private string GenerateToken(
        string clientIdentifier,
        DateTime created,
        DateTime expired, 
        Guid userId,
        string correlationId)
    {
        var config = CentralConfig.GetMedinillaConfiguration();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.SigningSecretKey));
        var handler = new JsonWebTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor()
        {
            Issuer = "Medinilla",
            Audience = "Medinilla-Sub",
            IssuedAt = created,
            Expires = expired,
            Subject = new ClaimsIdentity(
            [
                new Claim("ClientIdentifier", clientIdentifier),
                new Claim("AuthorizationUserId", userId.ToString()),
                new Claim("CorrelationId", correlationId)
            ]),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };
        var token = handler.CreateToken(tokenDescriptor);
        return token;
    }
    
    public async Task<IdToken> GenerateCentralToken(
        string clientIdentifier,
        Guid authUserId,
        long durationSeconds,
        string correlationId)
    {
        ChargingStation chargingStation;
        try
        {
            chargingStation = await context.GetChargingStation(clientIdentifier);
        }
        catch (AggregateRootNotFoundException e)
        {
            logger.LogError(
                "{ci}: Failed to generate central token type. Client Identifier not found.",
                clientIdentifier);
            throw;
        }

        var authUser = chargingStation.AuthorizationUsers.FirstOrDefault(u => u.Id == authUserId);
        if (authUser is null)
        {
            logger.LogError(
                "{ci}: Failed to generate central Id Token: User with id \"{uid}\" not found.",
                clientIdentifier,
                authUserId);
            throw new AuthorizationUserNotFoundException();
        }

        var created = DateTime.UtcNow;
        var expiry = created.AddSeconds(durationSeconds);
        var rawToken = GenerateToken(clientIdentifier, created, expiry, authUser.Id, correlationId);

        var token = new DbToken()
        {
            AuthorizationUserId = authUserId,
            Blocked = false,
            CreatedDate = created,
            ExpiryDate = expiry,
            IdType = DbIdTokenType.Central,
            Token = rawToken,
        };
        chargingStation.IdTokens.Add(token);

        await context.SaveChangesAsync();

        return MapToContractToken(token);
    }

    public async Task ReleaseCentralToken(DbToken token)
    {
        token.Blocked = true;
        await context.SaveChangesAsync();
    }
}