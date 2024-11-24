using Medinilla.DataTypes.Contracts.Common;
using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts;

public sealed class AuthorizeRequest
{
    public string? Certificate { get; set; }

    public IdToken IdToken { get; set; }

    [JsonPropertyName("iso15118CertificateHashData")]
    public OcspRequestData? ISO15118CertificateHashData { get; set; }
}
