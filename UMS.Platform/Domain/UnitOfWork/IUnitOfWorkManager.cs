using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs;
using UMS.Platform.Common.Extensions;

namespace UMS.Platform.Domain.UnitOfWork;

/// <summary>
///     Unit of work manager.
///     Used to begin and control a unit of work.
/// </summary>
public interface IUnitOfWorkManager : IDisposable
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IUnitOfWorkManager)}");

    /// <summary>
    ///     A single separated global uow in current scoped is used by repository for read data using query, usually when need
    ///     to return data
    ///     as enumerable to help download data like streaming data (not load all big data into ram) <br />
    ///     or any other purpose that just want to using query directly without think about uow of the query. <br />
    ///     This uow is auto created once per scope when access it. <br />
    ///     This won't affect the normal current uow queue list when Begin a new uow.
    /// </summary>
    public IUnitOfWork GlobalUow { get; }

    public IPlatformCqrs CurrentSameScopeCqrs { get; }

    /// <summary>
    ///     Just create and return a new instance of uow without manage it. It will not affect to
    ///     <see cref="HasCurrentActiveUow" /> result
    /// </summary>
    public IUnitOfWork CreateNewUow();

    /// <summary>
    ///     Gets last unit of work (or null if not exists).
    /// </summary>
    [return: MaybeNull]
    public IUnitOfWork CurrentUow();

    /// <summary>
    ///     Gets currently latest active unit of work.
    ///     <exception cref="Exception">Throw exception if there is not active unit of work.</exception>
    /// </summary>
    public IUnitOfWork CurrentActiveUow();

    /// <summary>
    ///     Gets currently latest or created active unit of work has id equal uowId.
    ///     <exception cref="Exception">Throw exception if there is not active unit of work.</exception>
    /// </summary>
    public IUnitOfWork CurrentOrCreatedActiveUow(string uowId);

    /// <summary>
    ///     Gets currently latest active unit of work of type <see cref="TUnitOfWork" />.
    ///     <exception cref="Exception">Throw exception if there is not active unit of work.</exception>
    /// </summary>
    public TUnitOfWork CurrentActiveUowOfType<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork;

    /// <summary>
    ///     Gets currently latest active unit of work. Return null if no active uow
    /// </summary>
    [return: MaybeNull]
    public IUnitOfWork TryGetCurrentActiveUow();

    /// <summary>
    ///     Gets currently latest or created active unit of work has id equal uowId. Return null if no active uow
    /// </summary>
    [return: MaybeNull]
    public IUnitOfWork TryGetCurrentOrCreatedActiveUow(string uowId);

    /// <summary>
    ///     Check that is there any currently latest active unit of work
    /// </summary>
    public bool HasCurrentActiveUow();

    /// <summary>
    ///     Check that is there any currently latest or created active unit of work has id equal uowId
    /// </summary>
    public bool HasCurrentOrCreatedActiveUow(string uowId);

    /// <summary>
    ///     Start a new unit of work. <br />
    ///     If current active unit of work is existing, return it. <br />
    ///     When suppressCurrentUow=true, new uow will be created even if current uow is existing. When false, use
    ///     current active uow if possible. <br />
    ///     Default is true.
    /// </summary>
    /// <param name="suppressCurrentUow">
    /// </param>
    public IUnitOfWork Begin(bool suppressCurrentUow = true);

    /// <summary>
    ///     Remove all managed inactive uow to clear memory
    /// </summary>
    public void RemoveAllInactiveUow();
}

public abstract class PlatformUnitOfWorkManager : IUnitOfWorkManager
{
    protected readonly List<IUnitOfWork> CurrentUnitOfWorks = new();
    protected readonly ConcurrentDictionary<string, IUnitOfWork> FreeCreatedUnitOfWorks = new();
    protected readonly SemaphoreSlim RemoveAllInactiveUowLock = new(1, 1);
    protected readonly IPlatformRootServiceProvider RootServiceProvider;
    private bool disposed;
    private bool disposing;

    private IUnitOfWork globalUow;

    protected PlatformUnitOfWorkManager(IPlatformCqrs currentSameScopeCqrs,
        IPlatformRootServiceProvider rootServiceProvider)
    {
        CurrentSameScopeCqrs = currentSameScopeCqrs;
        RootServiceProvider = rootServiceProvider;
    }

    public IPlatformCqrs CurrentSameScopeCqrs { get; }

    public abstract IUnitOfWork CreateNewUow();

    public virtual IUnitOfWork CurrentUow()
    {
        RemoveAllInactiveUow();

        return CurrentUnitOfWorks.LastOrDefault();
    }

    public IUnitOfWork CurrentActiveUow()
    {
        var currentUow = CurrentUow();

        EnsureUowActive(currentUow);

        return currentUow;
    }

    public IUnitOfWork CurrentOrCreatedActiveUow(string uowId)
    {
        var currentUow = CurrentOrCreatedUow(uowId);

        EnsureUowActive(currentUow);

        return currentUow;
    }

    public IUnitOfWork TryGetCurrentActiveUow()
    {
        return CurrentUow()?.IsActive() == true
            ? CurrentUow()
            : null;
    }

    public IUnitOfWork TryGetCurrentOrCreatedActiveUow(string uowId)
    {
        if (uowId == null) return TryGetCurrentActiveUow();

        var currentOrCreatedUow = CurrentOrCreatedUow(uowId);

        return currentOrCreatedUow?.IsActive() == true
            ? currentOrCreatedUow
            : null;
    }

    public bool HasCurrentActiveUow()
    {
        return CurrentUow()?.IsActive() == true;
    }

    public bool HasCurrentOrCreatedActiveUow(string uowId)
    {
        return CurrentOrCreatedUow(uowId)?.IsActive() == true;
    }

    public virtual IUnitOfWork Begin(bool suppressCurrentUow = true)
    {
        RemoveAllInactiveUow();

        if (suppressCurrentUow || CurrentUnitOfWorks.IsEmpty()) CurrentUnitOfWorks.Add(CreateNewUow());

        return CurrentUow();
    }

    public TUnitOfWork CurrentActiveUowOfType<TUnitOfWork>() where TUnitOfWork : class, IUnitOfWork
    {
        var uowOfType = CurrentUow()?.UowOfType<TUnitOfWork>();

        return uowOfType
            .Ensure(currentUow => currentUow != null,
                $"There's no current any uow of type {typeof(TUnitOfWork).FullName} has been begun.")
            .Ensure(currentUow => currentUow.IsActive(),
                $"Current unit of work of type {typeof(TUnitOfWork).FullName} has been completed or disposed.");
    }

    public IUnitOfWork GlobalUow => globalUow ??= CreateNewUow();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void RemoveAllInactiveUow()
    {
        if (disposed || disposing) return;

        try
        {
            RemoveAllInactiveUowLock.Wait();

            CurrentUnitOfWorks.RemoveWhere(p => !p.IsActive(), out _);
            FreeCreatedUnitOfWorks.Keys.Where(key => !FreeCreatedUnitOfWorks[key].IsActive())
                .ForEach(inactivatedUowKey => FreeCreatedUnitOfWorks.TryRemove(inactivatedUowKey, out _));
        }
        finally
        {
            RemoveAllInactiveUowLock.Release();
        }
    }

    public virtual IUnitOfWork CurrentOrCreatedUow(string uowId)
    {
        RemoveAllInactiveUow();

        return LastOrDefaultMatchedUowOfId(CurrentUnitOfWorks, uowId) ??
               LastOrDefaultMatchedUowOfId(FreeCreatedUnitOfWorks.Values.ToList(), uowId);

        static IUnitOfWork LastOrDefaultMatchedUowOfId(List<IUnitOfWork> unitOfWorks, string uowId)
        {
            for (var i = unitOfWorks.Count - 1; i >= 0; i--)
            {
                var matchedUow = unitOfWorks.ElementAtOrDefault(i)?.UowOfId(uowId);

                if (matchedUow != null) return matchedUow;
            }

            return null;
        }
    }

    private static void EnsureUowActive(IUnitOfWork currentUow)
    {
        currentUow
            .Ensure(currentUow => currentUow != null,
                "There's no current any uow has been begun.")
            .Ensure(currentUow => currentUow.IsActive(),
                "Current unit of work has been completed or disposed.");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;

        this.disposing = true;

        if (disposing)
        {
            // free managed resources. ToList to clone the list to dispose because dispose could cause trigger RemoveAllInactiveUow => modified the original list
            CurrentUnitOfWorks.ToList().ForEach(currentUnitOfWork => currentUnitOfWork?.Dispose());
            CurrentUnitOfWorks.Clear();

            // free managed resources. ToList to clone the list to dispose because dispose could cause trigger RemoveAllInactiveUow => modified the original list
            FreeCreatedUnitOfWorks.ToList().ForEach(currentUnitOfWork => currentUnitOfWork.Value?.Dispose());
            FreeCreatedUnitOfWorks.Clear();

            globalUow?.Dispose();

            RemoveAllInactiveUowLock.Dispose();
        }

        disposed = true;
        this.disposing = false;
    }
}

public static class UnitOfWorkManagerExtension
{
    public static async Task ExecuteInNewUow(this IUnitOfWorkManager unitOfWorkManager,
        Func<IUnitOfWork, Task> actionFn, bool suppressCurrentUow = true)
    {
        using (var uow = unitOfWorkManager.Begin(suppressCurrentUow))
        {
            await actionFn(uow);

            await uow.CompleteAsync();
        }
    }

    public static async Task<TResult> ExecuteInNewUow<TResult>(this IUnitOfWorkManager unitOfWorkManager,
        Func<IUnitOfWork, Task<TResult>> actionFn,
        bool suppressCurrentUow = true)
    {
        using (var uow = unitOfWorkManager.Begin(suppressCurrentUow))
        {
            var result = await actionFn(uow);

            await uow.CompleteAsync();

            return result;
        }
    }
}