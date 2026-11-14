namespace Medinilla.Webhooks.Service.Delivery;

public static class WebhookHeaders
{
    /// <summary>Stable event UUID in canonical D format, for consumer deduplication.</summary>
    public const string EventId = "X-Medinilla-Event-Id";

    /// <summary>The wrapper's EventType discriminator, included for routing/observability.</summary>
    public const string EventType = "X-Medinilla-Event-Type";

    /// <summary>Unix epoch seconds used in the signed input.</summary>
    public const string Timestamp = "X-Medinilla-Timestamp";

    /// <summary>
    /// <c>v1=&lt;lowercase hex HMAC-SHA256&gt;</c> over
    /// <c>UTF8(timestamp + ".") || exact request body bytes</c>.
    /// </summary>
    public const string Signature = "X-Medinilla-Signature";
}

public static class WebhookSigner
{
    public const string SignatureScheme = "v1";

    public static string ComputeSignature(
        string signingSecret,
        string timestamp,
        ReadOnlySpan<byte> body)
    {
        var prefix = System.Text.Encoding.UTF8.GetBytes(timestamp + ".");
        var signedInput = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signedInput, 0);
        body.CopyTo(signedInput.AsSpan(prefix.Length));

        var key = System.Text.Encoding.UTF8.GetBytes(signingSecret);
        var hash = System.Security.Cryptography.HMACSHA256.HashData(key, signedInput);

        return $"{SignatureScheme}={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
