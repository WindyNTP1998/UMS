using UMS.Platform.Infrastructures.Abstract;

namespace UMS.Platform.Infrastructures.PushNotification;

public interface IPushNotificationPlatformService : IPlatformInfrastructureService
{
    public Task SendAsync(PushNotificationPlatformMessage message, CancellationToken cancellationToken);
}