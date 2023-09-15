using Microsoft.Extensions.DependencyInjection;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Persistence.Domain;

public class PlatformDefaultPersistenceUnitOfWorkManager : PlatformUnitOfWorkManager
{
    protected readonly IServiceProvider ServiceProvider;

    public PlatformDefaultPersistenceUnitOfWorkManager(IPlatformCqrs cqrs,
        IPlatformRootServiceProvider rootServiceProvider,
        IServiceProvider serviceProvider) : base(cqrs, rootServiceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public override IUnitOfWork CreateNewUow()
    {
        // Doing create scope because IUnitOfWork resolve with DbContext, and DbContext lifetime is usually scoped to support resolve db context
        // to use it directly in application layer in some project or cases without using repository.
        // But we still want to support Uow create new like transient, each uow associated with new db context
        // So that we can begin/destroy uow separately

        var newScope = ServiceProvider.CreateScope();

        var uow = new PlatformAggregatedPersistenceUnitOfWork(RootServiceProvider,
                newScope.ServiceProvider.GetServices<IUnitOfWork>()
                    .Select(p => p.With(_ => _.CreatedByUnitOfWorkManager = this))
                    .ToList(),
                newScope)
            .With(uow => uow.CreatedByUnitOfWorkManager = this);

        uow.OnDisposedActions.Add(async () =>
            await Task.Run(() => uow.CreatedByUnitOfWorkManager.RemoveAllInactiveUow()));

        FreeCreatedUnitOfWorks.TryAdd(uow.Id, uow);

        return uow;
    }
}