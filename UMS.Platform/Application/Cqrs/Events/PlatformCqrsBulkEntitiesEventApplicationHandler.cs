using Microsoft.Extensions.Logging;
using UMS.Platform.Common;
using UMS.Platform.Domain.Entities;
using UMS.Platform.Domain.Events;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Application.Cqrs.Events;

public abstract class PlatformCqrsBulkEntitiesEventApplicationHandler<TEntity, TPrimaryKey>
    : PlatformCqrsEventApplicationHandler<PlatformCqrsBulkEntitiesEvent<TEntity, TPrimaryKey>>
    where TEntity : class, IEntity<TPrimaryKey>, new()
{
    protected PlatformCqrsBulkEntitiesEventApplicationHandler(ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory,
        unitOfWorkManager,
        serviceProvider,
        rootServiceProvider)
    {
    }
}