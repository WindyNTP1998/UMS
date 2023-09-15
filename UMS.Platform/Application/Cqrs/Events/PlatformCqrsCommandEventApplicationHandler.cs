using Microsoft.Extensions.Logging;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs.Commands;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Application.Cqrs.Events;

public abstract class PlatformCqrsCommandEventApplicationHandler<TCommand>
    : PlatformCqrsEventApplicationHandler<PlatformCqrsCommandEvent<TCommand>>
    where TCommand : class, IPlatformCqrsCommand, new()
{
    protected PlatformCqrsCommandEventApplicationHandler(ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider)
    {
    }

    protected override bool HandleWhen(PlatformCqrsCommandEvent<TCommand> @event)
    {
        return @event.Action == PlatformCqrsCommandEventAction.Executed;
    }
}