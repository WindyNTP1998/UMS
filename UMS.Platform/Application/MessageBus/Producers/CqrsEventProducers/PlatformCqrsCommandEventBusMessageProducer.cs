using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context;
using UMS.Platform.Application.Context.UserContext;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs.Commands;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.Producers.CqrsEventProducers;

public abstract class PlatformCqrsCommandEventBusMessageProducer<TCommand>
    : PlatformCqrsEventBusMessageProducer<PlatformCqrsCommandEvent<TCommand>,
        PlatformCqrsCommandEventBusMessage<TCommand>>
    where TCommand : class, IPlatformCqrsCommand, new()
{
    protected PlatformCqrsCommandEventBusMessageProducer(ILoggerFactory loggerFactory,
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

    protected override PlatformCqrsCommandEventBusMessage<TCommand> BuildMessage(
        PlatformCqrsCommandEvent<TCommand> @event)
    {
        return PlatformCqrsCommandEventBusMessage<TCommand>.New<PlatformCqrsCommandEventBusMessage<TCommand>>(
            Guid.NewGuid().ToString(),
            @event,
            BuildPlatformEventBusMessageIdentity(),
            ApplicationSettingContext.ApplicationName,
            PlatformCqrsCommandEvent.EventTypeValue,
            @event.EventAction,
            UserContextAccessor.Current.GetAllKeyValues());
    }
}

public class PlatformCqrsCommandEventBusMessage<TCommand> : PlatformBusMessage<PlatformCqrsCommandEvent<TCommand>>
    where TCommand : class, IPlatformCqrsCommand, new()
{
}