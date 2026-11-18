using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.Interfaces.Services;

public interface ISecurityEventNotificationService
{
    Task ProcessNotification(string clientIdentifier, SecurityEventNotificationRequest request);
}
