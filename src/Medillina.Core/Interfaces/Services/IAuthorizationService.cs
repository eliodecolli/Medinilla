using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.Interfaces.Services;

public enum AuthorizationProcessingStatus
{
    Completed,
    UnsupportedCertificate,
    ChargingStationNotFound,
    AuthorizationError,
}

public sealed record AuthorizationProcessingResult(
    AuthorizationProcessingStatus Status,
    AuthorizeResponse? Response = null,
    string? ErrorMessage = null)
{
    public static AuthorizationProcessingResult Completed(AuthorizeResponse response) =>
        new(AuthorizationProcessingStatus.Completed, response);

    public static AuthorizationProcessingResult UnsupportedCertificate() =>
        new(AuthorizationProcessingStatus.UnsupportedCertificate);

    public static AuthorizationProcessingResult ChargingStationNotFound() =>
        new(AuthorizationProcessingStatus.ChargingStationNotFound);

    public static AuthorizationProcessingResult AuthorizationError(string message) =>
        new(AuthorizationProcessingStatus.AuthorizationError, ErrorMessage: message);
}

public interface IAuthorizationService
{
    Task<AuthorizationProcessingResult> ProcessAuthorization(
        string clientIdentifier,
        AuthorizeRequest request);
}
