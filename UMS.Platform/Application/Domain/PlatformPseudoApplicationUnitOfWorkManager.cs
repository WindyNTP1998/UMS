using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Application.Domain;

internal sealed class PlatformPseudoApplicationUnitOfWorkManager : PlatformUnitOfWorkManager
{
    public PlatformPseudoApplicationUnitOfWorkManager(IPlatformCqrs currentSameScopeCqrs,
        IPlatformRootServiceProvider rootServiceProvider) : base(currentSameScopeCqrs, rootServiceProvider)
    {
    }

    public override IUnitOfWork CreateNewUow()
    {
        return new PlatformPseudoApplicationUnitOfWork(RootServiceProvider)
            .With(_ => _.CreatedByUnitOfWorkManager = this);
    }
}

internal sealed class PlatformPseudoApplicationUnitOfWork : PlatformUnitOfWork
{
    public PlatformPseudoApplicationUnitOfWork(IPlatformRootServiceProvider rootServiceProvider) : base(
        rootServiceProvider)
    {
    }

    public override bool IsPseudoTransactionUow()
    {
        return true;
    }

    public override bool MustKeepUowForQuery()
    {
        return false;
    }

    public override bool DoesSupportParallelQuery()
    {
        return true;
    }
}