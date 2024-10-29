namespace Medinilla.DataTypes.Contracts;

public sealed class SecurityEventNotificationRequest
{

    public string TechInfo { get; set; }

    public string Type { get; set; }

    public DateTime Timestamp { get; set; }
}
