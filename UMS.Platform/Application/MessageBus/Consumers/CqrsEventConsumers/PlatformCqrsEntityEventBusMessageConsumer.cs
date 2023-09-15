using Microsoft.Extensions.Logging;
using UMS.Platform.Common;
using UMS.Platform.Domain.Entities;
using UMS.Platform.Domain.Events;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.Consumers.CqrsEventConsumers;

public interface IPlatformCqrsEntityEventBusMessageConsumer<in TMessage, TEntity>
    : IPlatformApplicationMessageBusConsumer<TMessage>
    where TEntity : class, IEntity, new()
    where TMessage : class, IPlatformWithPayloadBusMessage<PlatformCqrsEntityEvent<TEntity>>,
    IPlatformSelfRoutingKeyBusMessage, IPlatformTrackableBusMessage, new()
{
}

public abstract class PlatformCqrsEntityEventBusMessageConsumer<TMessage, TEntity>
    : PlatformApplicationMessageBusConsumer<TMessage>
    where TEntity : class, IEntity, new()
    where TMessage : class, IPlatformWithPayloadBusMessage<PlatformCqrsEntityEvent<TEntity>>,
    IPlatformSelfRoutingKeyBusMessage, IPlatformTrackableBusMessage, new()
{
    protected PlatformCqrsEntityEventBusMessageConsumer(ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, uowManager, serviceProvider,
        rootServiceProvider)
    {
    }
}