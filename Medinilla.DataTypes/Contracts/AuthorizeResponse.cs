using Medinilla.DataTypes.Contracts.Common;

namespace Medinilla.DataTypes.Contracts;

public sealed class AuthorizeResponse
{
    public AuthorizeCertificateStatus? CertificateStatus { get; set; }

    public IdTokenInfo IdTokenInfo { get; set; }
}
