using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Accepted values for Message Format
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageFormatEnum
{
    /// <summary>
    /// Message format in ASCII text
    /// </summary>
    ASCII,

    /// <summary>
    /// Message format in HTML
    /// </summary>
    HTML,

    /// <summary>
    /// Message format as URI
    /// </summary>
    URI,

    /// <summary>
    /// Message format in UTF-8
    /// </summary>
    UTF8
}