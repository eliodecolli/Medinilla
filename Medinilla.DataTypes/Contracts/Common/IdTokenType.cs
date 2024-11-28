using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Enumeration of possible idToken types
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IdTokenType
{
    /// <summary>
    /// Central system authorization
    /// </summary>
    Central,

    /// <summary>
    /// ISO 15118 Electric Vehicle Contract ID
    /// </summary>
    EMAID,

    /// <summary>
    /// ISO/IEC 14443 Type A tag
    /// </summary>
    ISO14443,

    /// <summary>
    /// ISO/IEC 15693 tag
    /// </summary>
    ISO15693,

    /// <summary>
    /// Entered key code
    /// </summary>
    KeyCode,

    /// <summary>
    /// Local authorization list
    /// </summary>
    Local,

    /// <summary>
    /// MAC address
    /// </summary>
    MacAddress,

    /// <summary>
    /// No authorization required
    /// </summary>
    NoAuthorization
}
