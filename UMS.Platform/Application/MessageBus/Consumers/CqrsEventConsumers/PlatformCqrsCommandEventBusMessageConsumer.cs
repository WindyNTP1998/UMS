using Microsoft.Extensions.Logging;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs.Commands;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.Consumers.CqrsEventConsumers;

public interface IPlatformCqrsCommandEventBusMessageConsumer<TCommand>
    : IPlatformApplicationMessageBusConsumer<PlatformBusMessage<PlatformCqrsCommandEvent<TCommand>>>
    where TCommand : class, IPlatformCqrsCommand, new()
{
}

public abstract class PlatformCqrsCommandEventBusMessageConsumer<TCommand>
    : PlatformApplicationMessageBusConsumer<PlatformBusMessage<PlatformCqrsCommandEvent<TCommand>>>,
        IPlatformCqrsCommandEventBusMessageConsumer<TCommand>
    where TCommand : class, IPlatformCqrsCommand, new()
{
    protected PlatformCqrsCommandEventBusMessageConsumer(ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, uowManager, serviceProvider,
        rootServiceProvider)
    {
    }
}