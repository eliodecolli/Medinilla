using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public sealed class AuthorizeResponse
{
    public string? CertificateStatus { get; set; }

    public IdTokenInfo IdTokenInfo { get; set; }
}
