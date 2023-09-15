using Microsoft.Extensions.Logging;
using UMS.Platform.Common;
using UMS.Platform.Domain.Events;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Application.Cqrs.Events;

public abstract class PlatformCqrsDomainEventApplicationHandler<TEvent> : PlatformCqrsEventApplicationHandler<TEvent>
    where TEvent : PlatformCqrsDomainEvent, new()
{
    protected PlatformCqrsDomainEventApplicationHandler(ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider)
    {
    }
}