using Medinilla.DataTypes.Contracts;

namespace Medinilla.Core.Interfaces.Services;

public interface IStatusNotificationService
{
    Task ProcessNotification(string clientIdentifier, StatusNotificationRequest request);
}
