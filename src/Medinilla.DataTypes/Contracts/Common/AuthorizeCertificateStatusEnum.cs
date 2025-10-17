using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Certificate status information.
/// - if all certificates are valid: return 'Accepted'.
/// - if one of the certificates was revoked, return 'CertificateRevoked'.
/// </summary>
public enum AuthorizeCertificateStatus
{
    /// <summary>
    /// All certificates are valid
    /// </summary>
    Accepted,

    /// <summary>
    /// Error with the certificate signature
    /// </summary>
    SignatureError,

    /// <summary>
    /// Certificate has expired
    /// </summary>
    CertificateExpired,

    /// <summary>
    /// One of the certificates was revoked
    /// </summary>
    CertificateRevoked,

    /// <summary>
    /// No certificate is available
    /// </summary>
    NoCertificateAvailable,

    /// <summary>
    /// Error in the certificate chain
    /// </summary>
    CertChainError,

    /// <summary>
    /// Contract associated with the certificate has been cancelled
    /// </summary>
    ContractCancelled
}