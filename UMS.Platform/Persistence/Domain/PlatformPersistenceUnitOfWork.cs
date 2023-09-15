using UMS.Platform.Application.Persistence;
using UMS.Platform.Common;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Persistence.Domain;

public interface IPlatformPersistenceUnitOfWork<out TDbContext> : IUnitOfWork
    where TDbContext : IPlatformDbContext
{
    public TDbContext DbContext { get; }
}

public abstract class PlatformPersistenceUnitOfWork<TDbContext>
    : PlatformUnitOfWork, IPlatformPersistenceUnitOfWork<TDbContext>
    where TDbContext : IPlatformDbContext
{
    public PlatformPersistenceUnitOfWork(IPlatformRootServiceProvider rootServiceProvider, TDbContext dbContext) : base(
        rootServiceProvider)
    {
        DbContext = dbContext.With(_ => _.MappedUnitOfWork = this);
    }

    public TDbContext DbContext { get; }

    // Protected implementation of Dispose pattern.
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Dispose managed state (managed objects).
        if (disposing) DbContext?.Dispose();

        Disposed = true;
    }
}