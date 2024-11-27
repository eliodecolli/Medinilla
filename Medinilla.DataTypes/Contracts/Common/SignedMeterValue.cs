namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Represents a signed version of the meter value
/// </summary>
public class SignedMeterValue
{
    /// <summary>
    /// Base64 encoded, contains the signed data which might contain more than just the meter value.
    /// It can contain information like timestamps, reference to a customer etc
    /// </summary>
    public string SignedMeterData { get; set; }

    /// <summary>
    /// Method used to create the digital signature
    /// </summary>
    public string SigningMethod { get; set; }

    /// <summary>
    /// Method used to encode the meter values before applying the digital signature algorithm
    /// </summary>
    public string EncodingMethod { get; set; }

    /// <summary>
    /// Base64 encoded, sending depends on configuration variable PublicKeyWithSignedMeterValue
    /// </summary>
    public string PublicKey { get; set; }
}