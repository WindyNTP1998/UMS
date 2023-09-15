using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context;
using UMS.Platform.Application.Context.UserContext;
using UMS.Platform.Common;
using UMS.Platform.Domain.Events;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.Producers.CqrsEventProducers;

public abstract class PlatformCqrsDomainEventBusMessageProducer<TDomainEvent>
    : PlatformCqrsEventBusMessageProducer<TDomainEvent, PlatformCqrsDomainEventBusMessage<TDomainEvent>>
    where TDomainEvent : PlatformCqrsDomainEvent, new()
{
    protected PlatformCqrsDomainEventBusMessageProducer(ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider,
        applicationBusMessageProducer,
        userContextAccessor,
        applicationSettingContext)
    {
    }

    protected override PlatformCqrsDomainEventBusMessage<TDomainEvent> BuildMessage(TDomainEvent @event)
    {
        return PlatformCqrsDomainEventBusMessage<TDomainEvent>.New<PlatformCqrsDomainEventBusMessage<TDomainEvent>>(
            Guid.NewGuid().ToString(),
            @event,
            BuildPlatformEventBusMessageIdentity(),
            ApplicationSettingContext.ApplicationName,
            PlatformCqrsDomainEvent.EventTypeValue,
            @event.EventAction,
            UserContextAccessor.Current.GetAllKeyValues());
    }
}

public class PlatformCqrsDomainEventBusMessage<TDomainEvent> : PlatformBusMessage<TDomainEvent>
    where TDomainEvent : PlatformCqrsDomainEvent, new()
{
}