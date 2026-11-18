using Medinilla.Core.Interfaces;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Logic.Authorization;
using Medinilla.Core.v1.Services;
using Medinilla.DataAccess.Relational;
using Medinilla.DataAccess.Relational.Enums;
using Medinilla.DataAccess.Relational.Models.Authorization;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.Infrastructure.Core.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ChargingStation = Medinilla.DataAccess.Relational.Models.ChargingStation;
using DbIdToken = Medinilla.DataAccess.Relational.Models.Authorization.IdToken;
using DbIdTokenType = Medinilla.DataAccess.Relational.Enums.IdTokenType;
using OcppIdToken = Medinilla.DataTypes.Contracts.Common.IdToken;
using OcppIdTokenType = Medinilla.DataTypes.Contracts.Common.IdTokenType;

namespace Medinilla.Core.Tests;

public class AuthorizationServiceShould : IDisposable
{
    private readonly TestDbContext _context;
    private readonly Mock<IAuthAlgorithm> _algorithm = new(MockBehavior.Strict);

    public AuthorizationServiceShould()
    {
        var options = new DbContextOptionsBuilder<MedinillaOcppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new TestDbContext(options);
        _algorithm.SetupGet(a => a.Algorithm).Returns(AuthorizationAlgorithm.Default);
        _algorithm.SetupGet(a => a.Priority).Returns(0);
        _algorithm.Setup(a => a.Authorize(It.IsAny<AuthorizationContext>()))
            .ReturnsAsync(AuthorizeStatus.Accepted);
    }

    [Fact]
    public async Task ResolveAKnownTokenAndPreserveThePipelineOutcome()
    {
        var station = await AddStation();
        station.IdTokens.Add(new DbIdToken { Token = "token-1", IdType = DbIdTokenType.ISO14443 });
        await _context.SaveChangesAsync();
        var request = CreateRequest("token-1");

        var outcome = await CreateService().ProcessAuthorization("station-1", request);

        Assert.Equal(AuthorizationProcessingStatus.Completed, outcome.Status);
        Assert.NotNull(outcome.Response);
        Assert.Equal(AuthorizeStatus.Accepted, outcome.Response.IdTokenInfo.Status);
        Assert.Equal(AuthorizeCertificateStatus.NoCertificateAvailable, outcome.Response.CertificateStatus);
        Assert.NotNull(outcome.Response.IdTokenInfo.CacheExpiryDateTime);
        _algorithm.Verify(a => a.Authorize(It.IsAny<AuthorizationContext>()), Times.Once);
    }

    [Fact]
    public async Task ReportUnknownForASuppliedTokenThatIsNotRegistered()
    {
        await AddStation();
        var request = CreateRequest("unknown-token");

        var outcome = await CreateService().ProcessAuthorization("station-1", request);

        Assert.Equal(AuthorizationProcessingStatus.Completed, outcome.Status);
        Assert.Equal(AuthorizeStatus.Unknown, outcome.Response!.IdTokenInfo.Status);
        _algorithm.Verify(a => a.Authorize(It.IsAny<AuthorizationContext>()), Times.Never);
    }

    [Fact]
    public async Task RejectCertificateAuthorizationWithoutRunningThePipeline()
    {
        await AddStation();
        var request = CreateRequest("token-1");
        request.Certificate = "certificate";

        var outcome = await CreateService().ProcessAuthorization("station-1", request);

        Assert.Equal(AuthorizationProcessingStatus.UnsupportedCertificate, outcome.Status);
        _algorithm.Verify(a => a.Authorize(It.IsAny<AuthorizationContext>()), Times.Never);
    }

    [Fact]
    public async Task SurfaceKnownAuthorizationFailuresAsTypedOutcomes()
    {
        await AddStation();
        _algorithm.Setup(a => a.Authorize(It.IsAny<AuthorizationContext>()))
            .ThrowsAsync(new OcppAuthorizationException("authorization failed"));

        var outcome = await CreateService().ProcessAuthorization("station-1", CreateRequest(null));

        Assert.Equal(AuthorizationProcessingStatus.AuthorizationError, outcome.Status);
        Assert.Equal("authorization failed", outcome.ErrorMessage);
    }

    [Fact]
    public async Task ReportAMissingChargingStationAsATypedOutcome()
    {
        var outcome = await CreateService().ProcessAuthorization("unknown-station", CreateRequest(null));

        Assert.Equal(AuthorizationProcessingStatus.ChargingStationNotFound, outcome.Status);
    }

    public void Dispose() => _context.Dispose();

    private AuthorizationService CreateService() => new(
        _context,
        NullLogger<AuthorizationService>.Instance,
        new AuthorizationAlgorithmFactory(
            [_algorithm.Object],
            NullLogger<AuthorizationAlgorithmFactory>.Instance));

    private async Task<ChargingStation> AddStation()
    {
        var station = new ChargingStation
        {
            Id = Guid.NewGuid(),
            ClientIdentifier = "station-1",
            Model = "Model",
            Vendor = "Vendor",
            LatestBootNotificationReason = "PowerUp",
        };
        _context.Add(station);
        await _context.SaveChangesAsync();
        return station;
    }

    private static AuthorizeRequest CreateRequest(string? token) => new()
    {
        IdToken = token is null
            ? null!
            : new OcppIdToken { Token = token, Type = OcppIdTokenType.ISO14443 },
    };

    private sealed class TestDbContext(DbContextOptions<MedinillaOcppDbContext> options)
        : MedinillaOcppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<AuthorizationDetails>().Ignore(details => details.AuthBlob);
        }
    }
}
