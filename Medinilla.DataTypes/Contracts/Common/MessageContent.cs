namespace Medinilla.DataTypes.Contracts.Common;

/// <summary>
/// Representation of a Message Content for displaying on a Charging Station
/// </summary>
public class MessageContent
{
    /// <summary>
    /// Format of the message
    /// </summary>
    public MessageFormatEnum Format { get; set; }

    /// <summary>
    /// Message language identifier. Contains a language code as defined in RFC5646
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Message contents
    /// </summary>
    public string Content { get; set; }
}