using UMS.Platform.Domain.Repositories;

namespace UMS.Platform.Application.MessageBus.OutboxPattern;

public interface IPlatformOutboxBusMessageRepository
    : IPlatformQueryableRootRepository<PlatformOutboxBusMessage, string>
{
}