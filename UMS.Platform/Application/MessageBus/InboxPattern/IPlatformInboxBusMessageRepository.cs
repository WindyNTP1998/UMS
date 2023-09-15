using UMS.Platform.Domain.Repositories;

namespace UMS.Platform.Application.MessageBus.InboxPattern;

public interface IPlatformInboxBusMessageRepository : IPlatformQueryableRootRepository<PlatformInboxBusMessage, string>
{
}