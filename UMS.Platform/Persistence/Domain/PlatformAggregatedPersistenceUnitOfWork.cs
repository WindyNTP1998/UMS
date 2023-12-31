using Microsoft.Extensions.DependencyInjection;
using UMS.Platform.Common;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Persistence.Domain;

public interface IPlatformAggregatedPersistenceUnitOfWork : IUnitOfWork
{
    public bool IsPseudoTransactionUow<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IUnitOfWork;
    public bool MustKeepUowForQuery<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IUnitOfWork;
    public bool DoesSupportParallelQuery<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IUnitOfWork;
}

/// <summary>
///     The aggregated unit of work is to support multi database type in a same application.
///     Each item in InnerUnitOfWorks present a REAL unit of work including a db context
/// </summary>
public class PlatformAggregatedPersistenceUnitOfWork : PlatformUnitOfWork, IPlatformAggregatedPersistenceUnitOfWork
{
    /// <summary>
    ///     Store associatedServiceScope to destroy it when uow is create, using and destroy
    /// </summary>
    private IServiceScope associatedServiceScope;

    public PlatformAggregatedPersistenceUnitOfWork(IPlatformRootServiceProvider rootServiceProvider,
        List<IUnitOfWork> innerUnitOfWorks,
        IServiceScope associatedServiceScope) : base(rootServiceProvider)
    {
        InnerUnitOfWorks = innerUnitOfWorks?
                               .Select(innerUow => innerUow.With(_ => _.ParentUnitOfWork = this))
                               .ToList() ??
                           new List<IUnitOfWork>();
        this.associatedServiceScope = associatedServiceScope;
    }

    public override bool IsPseudoTransactionUow()
    {
        return InnerUnitOfWorks.All(p => p.IsPseudoTransactionUow());
    }

    public override bool MustKeepUowForQuery()
    {
        return InnerUnitOfWorks.Any(p => p.MustKeepUowForQuery());
    }

    public override bool DoesSupportParallelQuery()
    {
        return InnerUnitOfWorks.All(p => p.DoesSupportParallelQuery());
    }

    public bool IsPseudoTransactionUow<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IUnitOfWork
    {
        return InnerUnitOfWorks.FirstOrDefault(p => p.Equals(uow))?.IsPseudoTransactionUow() == true;
    }

    public bool MustKeepUowForQuery<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IUnitOfWork
    {
        return InnerUnitOfWorks.FirstOrDefault(p => p.Equals(uow))?.MustKeepUowForQuery() == true;
    }

    public bool DoesSupportParallelQuery<TInnerUnitOfWork>(TInnerUnitOfWork uow) where TInnerUnitOfWork : IUnitOfWork
    {
        return InnerUnitOfWorks.FirstOrDefault(p => p.Equals(uow))?.DoesSupportParallelQuery() == true;
    }

    public override bool IsActive()
    {
        return base.IsActive() && InnerUnitOfWorks.Any(p => p.IsActive());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            InnerUnitOfWorks.ForEach(p => p.Dispose());
            InnerUnitOfWorks.Clear();

            if (associatedServiceScope != null)
            {
                var associatedServiceScopeToDispose = associatedServiceScope;

                associatedServiceScope = null;

                associatedServiceScopeToDispose.Dispose();
            }
        }

        Disposed = true;
    }
}