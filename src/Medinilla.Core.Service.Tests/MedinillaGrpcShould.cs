using Medinilla.Core.gRPC.Service;
using Medinilla.Core.Interfaces;
using Medinilla.Core.Interfaces.Services;
using Medinilla.Core.Service.Communication;
using Medinilla.Core.Service.Exceptions;
using Medinilla.Core.v1.Exceptions;
using Medinilla.DataAccess.Exceptions;
using Medinilla.Infrastructure.WAMP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OcppIdToken = Medinilla.DataTypes.Contracts.Common.IdToken;
using OcppIdTokenType = Medinilla.DataTypes.Contracts.Common.IdTokenType;

namespace Medinilla.Core.Service.Tests;

public class MedinillaGrpcShould : IDisposable
{
    private const string ClientIdentifier = "station-1";
    private static readonly Guid AuthorizationUserId = Guid.Parse("a37cb4ac-0445-41aa-a106-80224a3b2fbb");
    private readonly Mock<IIdTokenService> _tokens = new(MockBehavior.Strict);
    private readonly Mock<IDisposable> _disposableTokens;
    private readonly Mock<IOcppCallRouter> _router = new(MockBehavior.Strict);
    private readonly Mock<ILogger<MedinillaGrpc>> _logger = new();
    private readonly ServiceProvider _provider;
    private readonly MedinillaGrpc _service;

    public MedinillaGrpcShould()
    {
        _disposableTokens = _tokens.As<IDisposable>();
        _disposableTokens.Setup(s => s.Dispose());
        var services = new ServiceCollection();
        services.AddScoped(_ => _tokens.Object);
        services.AddScoped(_ => _router.Object);
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _service = new MedinillaGrpc(_logger.Object, _provider);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task RejectInvalidAuthorizationUserIdsBeforeGeneratingAToken(string userId)
    {
        var request = CreateRequest();
        request.AuthorizationUserId = userId;

        var response = await _service.RequestStartTransaction(request, null!);

        AssertFailure(response, "authorization_user_id must be a valid, non-empty GUID.");
        _tokens.VerifyNoOtherCalls();
        _router.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public async Task RejectInvalidDurationsBeforeGeneratingAToken(long durationSeconds)
    {
        var request = CreateRequest();
        request.DurationSeconds = durationSeconds;

        var response = await _service.RequestStartTransaction(request, null!);

        AssertFailure(response, "duration_seconds must be positive and produce a valid expiry date.");
        _tokens.VerifyNoOtherCalls();
        _router.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task RejectMissingClientIdentifiersBeforeGeneratingAToken(string clientIdentifier)
    {
        var request = CreateRequest();
        request.ClientIdentifier = clientIdentifier;

        var response = await _service.RequestStartTransaction(request, null!);

        AssertFailure(response, "client_identifier is required.");
        _tokens.VerifyNoOtherCalls();
        _router.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReturnAClearErrorWhenTheAuthorizationUserDoesNotExist()
    {
        GivenGenerationFails(new AuthorizationUserNotFoundException());

        var response = await _service.RequestStartTransaction(CreateRequest(), null!);

        AssertFailure(response, "Authorization user not found for this charging station.");
        _router.VerifyNoOtherCalls();
        _disposableTokens.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ReturnAClearErrorWhenTheChargingStationDoesNotExist()
    {
        GivenGenerationFails(new AggregateRootNotFoundException("Internal database lookup details"));

        var response = await _service.RequestStartTransaction(CreateRequest(), null!);

        AssertFailure(response, "Charging station not found.");
        _router.VerifyNoOtherCalls();
        _disposableTokens.Verify(s => s.Dispose(), Times.Once);
    }

    [Theory]
    [InlineData("configuration")]
    [InlineData("signing")]
    [InlineData("persistence")]
    public async Task LogUnexpectedGenerationFailuresWithoutLeakingTheirDetails(string failure)
    {
        const string privateDetails = "Sensitive internal configuration or database details";
        Exception exception = failure switch
        {
            "configuration" => new FileNotFoundException(privateDetails),
            "signing" => new ArgumentException(privateDetails),
            "persistence" => new DbUpdateException(privateDetails),
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };
        GivenGenerationFails(exception);

        var response = await _service.RequestStartTransaction(CreateRequest(), null!);

        AssertFailure(response, "Unable to start transaction due to an internal error.");
        _router.VerifyNoOtherCalls();
        _disposableTokens.Verify(s => s.Dispose(), Times.Once);
        VerifyErrorLogged(exception);
    }

    [Fact]
    public async Task SubmitTheGeneratedTokenAndReturnTheSameCorrelationId()
    {
        var token = GivenGeneratedToken();
        OcppCallRequest? submitted = null;
        _router.Setup(r => r.SubmitAsync(ClientIdentifier, It.IsAny<OcppCallRequest>()))
            .Callback<string, OcppCallRequest>((_, call) => submitted = call)
            .Returns(Task.CompletedTask);

        var response = await _service.RequestStartTransaction(CreateRequest(), null!);

        Assert.False(response.Error.HasError);
        Assert.Equal(token.Token, response.Token.Token);
        Assert.Equal("Central", response.Token.Type);
        Assert.Equal(token.AdditionalInfo!.AdditionalIdToken, response.Token.AdditionalInfo.AdditionalIdToken);
        Assert.Equal(token.AdditionalInfo.Type, response.Token.AdditionalInfo.Type);
        Assert.True(Guid.TryParse(response.CorrelationId, out _));
        _tokens.Verify(s => s.GenerateCentralToken(
            ClientIdentifier, AuthorizationUserId, 3600, response.CorrelationId), Times.Once);
        _router.Verify(r => r.SubmitAsync(ClientIdentifier, It.IsAny<OcppCallRequest>()), Times.Once);
        Assert.NotNull(submitted);
        Assert.Equal(response.CorrelationId, submitted.MessageId);
        Assert.Equal(OcppActionNames.RequestStartTransaction, submitted.Action);
        var payload = submitted.As<Medinilla.DataTypes.Contracts.RequestStartTransactionRequest>();
        Assert.Equal(token.Token, payload.IdToken.Token);
        Assert.Equal(OcppIdTokenType.Central, payload.IdToken.Type);
        Assert.Equal(2, payload.EvseId);
        Assert.Equal(42, payload.RemoteStartId);
        _disposableTokens.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public async Task PreserveTheNotConnectedErrorAndDisposeTheScope()
    {
        GivenGeneratedToken();
        _router.Setup(r => r.SubmitAsync(ClientIdentifier, It.IsAny<OcppCallRequest>()))
            .ThrowsAsync(new ChargerNotConnectedException(ClientIdentifier));

        var response = await _service.RequestStartTransaction(CreateRequest(), null!);

        Assert.True(response.Error.HasError);
        Assert.Contains("NotConnected", response.Error.Message);
        Assert.Null(response.Token);
        Assert.True(Guid.TryParse(response.CorrelationId, out _));
        _disposableTokens.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public async Task HideUnexpectedSubmissionErrorsAndDisposeTheScope()
    {
        GivenGeneratedToken();
        var exception = new InvalidOperationException("Internal routing details");
        _router.Setup(r => r.SubmitAsync(ClientIdentifier, It.IsAny<OcppCallRequest>()))
            .ThrowsAsync(exception);

        var response = await _service.RequestStartTransaction(CreateRequest(), null!);

        AssertFailure(response, "Unable to start transaction due to an internal error.");
        _disposableTokens.Verify(s => s.Dispose(), Times.Once);
        VerifyErrorLogged(exception);
    }

    private void GivenGenerationFails(Exception exception)
    {
        _tokens.Setup(s => s.GenerateCentralToken(
                ClientIdentifier, AuthorizationUserId, 3600, It.IsAny<string>()))
            .ThrowsAsync(exception);
    }

    private OcppIdToken GivenGeneratedToken()
    {
        var token = new OcppIdToken
        {
            Token = "generated-central-token",
            Type = OcppIdTokenType.Central,
            AdditionalInfo = new Medinilla.DataTypes.Contracts.Common.AdditionalTokenInfo
            {
                AdditionalIdToken = "additional-token",
                Type = "custom-type",
            },
        };
        _tokens.Setup(s => s.GenerateCentralToken(
                ClientIdentifier, AuthorizationUserId, 3600, It.IsAny<string>()))
            .ReturnsAsync(token);
        return token;
    }

    private static RequestStartTransactionRequest CreateRequest() => new()
    {
        ClientIdentifier = ClientIdentifier,
        AuthorizationUserId = AuthorizationUserId.ToString(),
        DurationSeconds = 3600,
        EvseId = 2,
        RemoteStartId = 42,
    };

    private static void AssertFailure(RequestStartTransactionResponse response, string message)
    {
        Assert.NotNull(response.Error);
        Assert.True(response.Error.HasError);
        Assert.Equal(message, response.Error.Message);
        Assert.Null(response.Token);
        Assert.True(Guid.TryParse(response.CorrelationId, out _));
    }

    private void VerifyErrorLogged(Exception exception) => _logger.Verify(l => l.Log(
        LogLevel.Error,
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        exception,
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);

    public void Dispose() => _provider.Dispose();
}
