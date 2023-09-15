using Microsoft.Extensions.Logging;
using UMS.Platform.Common;
using UMS.Platform.Domain.Entities;
using UMS.Platform.Domain.Events;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Application.Cqrs.Events;

public abstract class PlatformCqrsEntityEventApplicationHandler<TEntity>
    : PlatformCqrsEventApplicationHandler<PlatformCqrsEntityEvent<TEntity>>
    where TEntity : class, IEntity, new()
{
    protected PlatformCqrsEntityEventApplicationHandler(ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider)
    {
    }
}