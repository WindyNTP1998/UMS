using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context;
using UMS.Platform.Application.Context.UserContext;
using UMS.Platform.Common;
using UMS.Platform.Domain.Entities;
using UMS.Platform.Domain.Events;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.Producers.CqrsEventProducers;

public abstract class PlatformCqrsEntityEventBusMessageProducer<TMessage, TEntity>
    : PlatformCqrsEventBusMessageProducer<PlatformCqrsEntityEvent<TEntity>, TMessage>
    where TEntity : class, IEntity, new()
    where TMessage : class, IPlatformWithPayloadBusMessage<PlatformCqrsEntityEvent<TEntity>>,
    IPlatformSelfRoutingKeyBusMessage, IPlatformTrackableBusMessage, new()
{
    protected PlatformCqrsEntityEventBusMessageProducer(ILoggerFactory loggerFactory,
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

    protected override TMessage BuildMessage(PlatformCqrsEntityEvent<TEntity> @event)
    {
        return PlatformCqrsEntityEventBusMessage<TEntity>.New<TMessage>(@event.Id,
            @event,
            BuildPlatformEventBusMessageIdentity(),
            ApplicationSettingContext.ApplicationName,
            PlatformCqrsEntityEvent.EventTypeValue,
            @event.EventAction,
            UserContextAccessor.Current.GetAllKeyValues());
    }
}

public class PlatformCqrsEntityEventBusMessage<TEntity> : PlatformBusMessage<PlatformCqrsEntityEvent<TEntity>>
    where TEntity : class, IEntity, new()
{
}