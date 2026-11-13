using System.Text;
using System.Text.Json.Nodes;
using Medinilla.Core.Logic.Configuration;
using Medinilla.Core.v1.Exceptions;
using Medinilla.Core.v1.Services;
using Medinilla.DataAccess.Exceptions;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Models;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ContractIdTokenType = Medinilla.DataTypes.Contracts.Common.IdTokenType;
using DbIdToken = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;
using DbIdTokenType = Medinilla.DataAccess.Relational.Enums.IdTokenType;

namespace Medinilla.Core.Tests;

// CentralConfig reads a shared file. Do not run these tests alongside other collections.
[CollectionDefinition(nameof(IdTokenServiceConfigurationCollection), DisableParallelization = true)]
public class IdTokenServiceConfigurationCollection
{
}

[Collection(nameof(IdTokenServiceConfigurationCollection))]
public class IdTokenServiceShould : IDisposable
{
    private const string ClientIdentifier = "station-1";
    private const string CorrelationId = "test-correlation-1";
    private const string SigningKey = "test-only-signing-key-with-at-least-32-bytes-for-hmac-sha256";
    private readonly TestDbContext _context;
    private readonly IdTokenService _service;
    private readonly string _configurationPath;
    private readonly byte[] _originalConfiguration;

    public IdTokenServiceShould()
    {
        var options = new DbContextOptionsBuilder<MedinillaOcppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestDbContext(options);
        _service = new IdTokenService(NullLogger<IdTokenService>.Instance, _context);

        // Change only the copied test-output file, preserving all other settings.
        _configurationPath = Path.Combine(
            Path.GetDirectoryName(typeof(CentralConfig).Assembly.Location)!, "defaults.json");
        _originalConfiguration = File.ReadAllBytes(_configurationPath);
        var configuration = JsonNode.Parse(_originalConfiguration)!.AsObject();
        configuration[nameof(MedinillaConfiguration.SigningSecretKey)] = SigningKey;
        File.WriteAllText(_configurationPath, configuration.ToJsonString());
    }

    [Fact]
    public async Task ThrowWithoutSavingWhenTheChargingStationDoesNotExist()
    {
        await Assert.ThrowsAsync<AggregateRootNotFoundException>(() =>
            _service.GenerateCentralToken(ClientIdentifier, Guid.NewGuid(), 3600, CorrelationId));

        Assert.Equal(0, _context.SaveChangesCalls);
        Assert.Empty(await _context.Set<DbIdToken>().ToListAsync());
    }

    [Fact]
    public async Task ThrowWithoutSavingWhenTheAuthorizationUserDoesNotExist()
    {
        var (station, _) = await AddStation();
        var saveCalls = _context.SaveChangesCalls;

        await Assert.ThrowsAsync<AuthorizationUserNotFoundException>(() =>
            _service.GenerateCentralToken(ClientIdentifier, Guid.NewGuid(), 3600, CorrelationId));

        Assert.Equal(saveCalls, _context.SaveChangesCalls);
        Assert.Empty(station.IdTokens);
        Assert.Empty(await _context.Set<DbIdToken>().ToListAsync());
    }

    [Fact]
    public async Task RejectAnAuthorizationUserBelongingToAnotherStation()
    {
        var (station, _) = await AddStation();
        var (otherStation, otherUser) = await AddStation("station-2");
        var saveCalls = _context.SaveChangesCalls;

        await Assert.ThrowsAsync<AuthorizationUserNotFoundException>(() =>
            _service.GenerateCentralToken(ClientIdentifier, otherUser.Id, 3600, CorrelationId));

        Assert.Equal(saveCalls, _context.SaveChangesCalls);
        Assert.Empty(station.IdTokens);
        Assert.Empty(otherStation.IdTokens);
        Assert.Empty(await _context.Set<DbIdToken>().ToListAsync());
    }

    [Fact]
    public async Task PersistTheCentralTokenAndReturnItsContractRepresentation()
    {
        var (station, user) = await AddStation();
        var saveCalls = _context.SaveChangesCalls;

        var result = await _service.GenerateCentralToken(ClientIdentifier, user.Id, 3600, CorrelationId);

        var attached = Assert.Single(station.IdTokens);
        Assert.Same(user, attached.User);
        Assert.Contains(attached, user.Tokens);
        Assert.Equal(saveCalls + 1, _context.SaveChangesCalls);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal(ContractIdTokenType.Central, result.Type);
        Assert.Null(result.AdditionalInfo);
        Assert.Equal(attached.Token, result.Token);

        // Reload so the assertions check persistence, not only the tracked graph.
        _context.ChangeTracker.Clear();
        var stored = await _context.Set<DbIdToken>().AsNoTracking().SingleAsync();
        Assert.NotEqual(Guid.Empty, stored.Id);
        Assert.Equal(station.Id, stored.ChargingStationId);
        Assert.Equal(user.Id, stored.AuthorizationUserId);
        Assert.Equal(DbIdTokenType.Central, stored.IdType);
        Assert.False(stored.Blocked);
        Assert.Equal(result.Token, stored.Token);
        Assert.NotNull(stored.CreatedDate);
        Assert.NotNull(stored.ExpiryDate);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(60L)]
    [InlineData(3600L)]
    [InlineData(86400L)]
    public async Task UseUtcCreationTimeAndTheRequestedDuration(long durationSeconds)
    {
        var (_, user) = await AddStation();
        var before = DateTime.UtcNow;

        await _service.GenerateCentralToken(ClientIdentifier, user.Id, durationSeconds, CorrelationId);

        var after = DateTime.UtcNow;
        var stored = await _context.Set<DbIdToken>().AsNoTracking().SingleAsync();
        Assert.NotNull(stored.CreatedDate);
        Assert.NotNull(stored.ExpiryDate);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedDate.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, stored.ExpiryDate.Value.Kind);
        Assert.InRange(stored.CreatedDate.Value, before, after);
        Assert.Equal(stored.CreatedDate.Value.AddSeconds(durationSeconds), stored.ExpiryDate.Value);
    }

    [Fact]
    public async Task IncludeTheStationUserIssuerAudienceAndTimestampsInTheJwt()
    {
        var (_, user) = await AddStation();

        var result = await _service.GenerateCentralToken(ClientIdentifier, user.Id, 3600, CorrelationId);

        var stored = await _context.Set<DbIdToken>().AsNoTracking().SingleAsync();
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);
        Assert.Equal("Medinilla", jwt.Issuer);
        Assert.Equal("Medinilla-Sub", Assert.Single(jwt.Audiences));
        Assert.Equal(ClientIdentifier, jwt.GetPayloadValue<string>("ClientIdentifier"));
        Assert.Equal(user.Id.ToString(), jwt.GetPayloadValue<string>("AuthorizationUserId"));
        Assert.Equal(CorrelationId, jwt.GetPayloadValue<string>("CorrelationId"));
        Assert.Equal(EpochTime.GetIntDate(stored.CreatedDate!.Value), jwt.GetPayloadValue<long>("iat"));
        Assert.Equal(EpochTime.GetIntDate(stored.ExpiryDate!.Value), jwt.GetPayloadValue<long>("exp"));
        Assert.Equal(SecurityAlgorithms.HmacSha256Signature, jwt.Alg);
    }

    [Fact]
    public async Task SignTheJwtWithTheConfiguredKey()
    {
        var (_, user) = await AddStation();
        var result = await _service.GenerateCentralToken(ClientIdentifier, user.Id, 3600, CorrelationId);

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(
            result.Token, CreateValidationParameters(SigningKey));

        Assert.True(validation.IsValid, validation.Exception?.ToString());
    }

    [Fact]
    public async Task ProduceAJwtThatFailsValidationWithADifferentSigningKey()
    {
        var (_, user) = await AddStation();
        var result = await _service.GenerateCentralToken(ClientIdentifier, user.Id, 3600, CorrelationId);

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(
            result.Token, CreateValidationParameters(
                "a-different-test-signing-key-with-at-least-32-bytes-for-hmac-sha256"));

        Assert.False(validation.IsValid);
        Assert.IsAssignableFrom<SecurityTokenInvalidSignatureException>(validation.Exception);
    }

    [Fact]
    public async Task PreserveExistingTokensWhenGeneratingANewOne()
    {
        var (station, user) = await AddStation();
        var existing = new DbIdToken
        {
            Token = "existing-rfid-token",
            IdType = DbIdTokenType.ISO14443,
            User = user,
        };
        station.IdTokens.Add(existing);
        await _context.SaveChangesAsync();

        var result = await _service.GenerateCentralToken(ClientIdentifier, user.Id, 3600, CorrelationId);

        _context.ChangeTracker.Clear();
        var stored = await _context.Set<DbIdToken>().AsNoTracking().ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, t => t.Id == existing.Id && t.Token == "existing-rfid-token"
            && t.IdType == DbIdTokenType.ISO14443);
        Assert.Contains(stored, t => t.Token == result.Token && t.IdType == DbIdTokenType.Central);
    }

    [Fact]
    public async Task PropagatePersistenceFailuresInsteadOfReturningAToken()
    {
        var (_, user) = await AddStation();
        var expected = new DbUpdateException("Simulated save failure");
        _context.SaveException = expected;

        var actual = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _service.GenerateCentralToken(ClientIdentifier, user.Id, 3600, CorrelationId));

        Assert.Same(expected, actual);
        Assert.Empty(await _context.Set<DbIdToken>().AsNoTracking().ToListAsync());
    }

    private static TokenValidationParameters CreateValidationParameters(string signingKey) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "Medinilla",
        ValidateAudience = true,
        ValidAudience = "Medinilla-Sub",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RequireSignedTokens = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256Signature],
        TryAllIssuerSigningKeys = true,
    };

    private async Task<(ChargingStation Station, AuthorizationUser User)> AddStation(
        string clientIdentifier = ClientIdentifier)
    {
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            ClientIdentifier = clientIdentifier,
            Model = "Test model",
            Vendor = "Test vendor",
            LatestBootNotificationReason = "PowerUp",
        };
        var user = new AuthorizationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test driver",
            IsActive = true,
            ChargingStation = station,
            Tokens = new List<DbIdToken>(),
        };
        station.AuthorizationUsers.Add(user);
        _context.Add(station);
        await _context.SaveChangesAsync();
        return (station, user);
    }

    public void Dispose()
    {
        try
        {
            File.WriteAllBytes(_configurationPath, _originalConfiguration);
        }
        finally
        {
            _context.Dispose();
        }
    }

    private sealed class TestDbContext(DbContextOptions<MedinillaOcppDbContext> options)
        : MedinillaOcppDbContext(options)
    {
        public int SaveChangesCalls { get; private set; }
        public Exception? SaveException { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            if (SaveException is not null)
                throw SaveException;

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // PostgreSQL JSON storage is unrelated to these tests and unsupported by InMemory.
            modelBuilder.Entity<AuthorizationDetails>().Ignore(details => details.AuthBlob);
        }
    }
}
