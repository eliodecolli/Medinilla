using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

public sealed class AdditionalTokenInfo
{
    public string AdditionalIdToken { get; set; }

    public string Type { get; set; }
}

public sealed class IdToken
{
    [JsonPropertyName("idToken")]
    public string Token { get; set; }

    public IdTokenType Type { get; set; }

    public AdditionalTokenInfo? AdditionalInfo { get; set; }
}
