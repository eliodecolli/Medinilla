using Medinilla.Core.Actions.Ocpp201;
using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Medinilla.DataTypes.Contracts.Common;
using Medinilla.DataTypes.Core;
using Medinilla.Infrastructure;
using Medinilla.Infrastructure.WAMP;
using Moq;

namespace Medinilla.Core.Tests;

public class AuthorizeActionShould
{
    private readonly Mock<IAuthorizationService> _service = new();

    [Fact]
    public async Task WrapACompletedOutcomeInACallResult()
    {
        var response = new AuthorizeResponse
        {
            IdTokenInfo = new IdTokenInfo { Status = AuthorizeStatus.Accepted },
        };
        _service.Setup(s => s.ProcessAuthorization("station-1", It.IsAny<AuthorizeRequest>()))
            .ReturnsAsync(AuthorizationProcessingResult.Completed(response));

        var result = await CreateAction().Execute(CreateCall(), "station-1");

        Assert.Null(result.Error);
        Assert.NotNull(result.Result);
        var payload = result.Result.As<AuthorizeResponse>();
        Assert.Equal(AuthorizeStatus.Accepted, payload.IdTokenInfo.Status);
    }

    [Fact]
    public async Task PreserveTheNotSupportedErrorForCertificateAuthorization()
    {
        _service.Setup(s => s.ProcessAuthorization("station-1", It.IsAny<AuthorizeRequest>()))
            .ReturnsAsync(AuthorizationProcessingResult.UnsupportedCertificate());

        var result = await CreateAction().Execute(CreateCall(), "station-1");

        Assert.Null(result.Result);
        Assert.NotNull(result.Error);
        Assert.Equal(OcppCallError.ErrorCodes.NotSupported, result.Error.ErrorCode);
    }

    [Fact]
    public async Task PreserveKnownAuthorizationFailureAsAnInternalError()
    {
        _service.Setup(s => s.ProcessAuthorization("station-1", It.IsAny<AuthorizeRequest>()))
            .ReturnsAsync(AuthorizationProcessingResult.AuthorizationError("authorization failed"));

        var result = await CreateAction().Execute(CreateCall(), "station-1");

        Assert.Null(result.Result);
        Assert.NotNull(result.Error);
        Assert.Equal(OcppCallError.ErrorCodes.InternalError, result.Error.ErrorCode);
        Assert.Contains("authorization failed", result.Error.ErrorDescription);
    }

    [Fact]
    public async Task PreserveTheMissingChargingStationError()
    {
        _service.Setup(s => s.ProcessAuthorization("station-1", It.IsAny<AuthorizeRequest>()))
            .ReturnsAsync(AuthorizationProcessingResult.ChargingStationNotFound());

        var result = await CreateAction().Execute(CreateCall(), "station-1");

        Assert.Null(result.Result);
        Assert.NotNull(result.Error);
        Assert.Equal(OcppCallError.ErrorCodes.InternalError, result.Error.ErrorCode);
        Assert.Contains("station-1", result.Error.ErrorDescription);
    }

    private AuthorizeAction CreateAction() => new(_service.Object);

    private static OcppCallRequest CreateCall()
    {
        var request = new AuthorizeRequest
        {
            IdToken = new IdToken { Token = "token-1", Type = IdTokenType.ISO14443 },
        };

        return new OcppCallRequest(
            "message-1",
            OcppActionNames.Authorize,
            OcppPayloadSerializer.SerializePayload(request));
    }
}
