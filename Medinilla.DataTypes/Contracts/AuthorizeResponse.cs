using Medinilla.DataTypes.Contracts.Common;
using System.ComponentModel;
using static System.Collections.Specialized.BitVector32;

namespace Medinilla.DataTypes.Contracts;

public sealed class AuthorizeResponse
{
    public string? CertificateStatus { get; set; }

    public IdTokenInfo IdTokenInfo { get; set; }
}
