using Microsoft.Extensions.Logging;
using UMS.Platform.Common;
using UMS.Platform.Domain.Events;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.Consumers.CqrsEventConsumers;

public interface IPlatformCqrsDomainEventBusMessageConsumer<TDomainEvent>
    : IPlatformApplicationMessageBusConsumer<PlatformBusMessage<TDomainEvent>>
    where TDomainEvent : PlatformCqrsDomainEvent, new()
{
}

public abstract class PlatformCqrsDomainEventBusMessageConsumer<TDomainEvent>
    : PlatformApplicationMessageBusConsumer<PlatformBusMessage<TDomainEvent>>,
        IPlatformCqrsDomainEventBusMessageConsumer<TDomainEvent>
    where TDomainEvent : PlatformCqrsDomainEvent, new()
{
    protected PlatformCqrsDomainEventBusMessageConsumer(ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, uowManager, serviceProvider,
        rootServiceProvider)
    {
    }
}