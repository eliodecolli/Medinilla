using Medinilla.Core.Interfaces.Services;
using Medinilla.DataTypes.Contracts;
using Microsoft.Extensions.Logging;

namespace Medinilla.Core.v1.Services;

public sealed class SecurityEventNotificationService(
    ILogger<SecurityEventNotificationService> logger) : ISecurityEventNotificationService
{
    public Task ProcessNotification(
        string clientIdentifier,
        SecurityEventNotificationRequest request)
    {
        logger.LogInformation(
            "{0} Received Security Notification Request: \"{1}\" with type \"{2}\"",
            clientIdentifier,
            request.TechInfo,
            request.Type);

        return Task.CompletedTask;
    }
}
