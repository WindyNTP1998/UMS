using System.Diagnostics;
using System.Linq.Expressions;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Utils;
using UMS.Platform.Domain.Entities;
using UMS.Platform.Domain.Events;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Domain.Repositories;

/// <summary>
///     This interface is used for conventional register class mapping via PlatformPersistenceModule.InternalRegister
/// </summary>
public interface IPlatformRepository
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformRepository)}");

    /// <summary>
    ///     Return current active uow. May throw exception if not existing one.
    /// </summary>
    public IUnitOfWork CurrentActiveUow();

    /// <summary>
    ///     Return current or created active uow which has uowId equal given uowId. May throw exception if not existing one.
    /// </summary>
    public IUnitOfWork CurrentOrCreatedActiveUow(string uowId);

    public IUnitOfWorkManager UowManager();
}

public interface IPlatformRepository<TEntity, TPrimaryKey> : IPlatformRepository
    where TEntity : class, IEntity<TPrimaryKey>, new()
{
    public Task<TEntity> GetByIdAsync(TPrimaryKey id, CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<List<TEntity>> GetByIdsAsync(List<TPrimaryKey> ids,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<List<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<TEntity> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default);

    public IEnumerable<TEntity> GetAllEnumerable(Expression<Func<TEntity, bool>> predicate = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);
}

public interface IPlatformRootRepository<TEntity, TPrimaryKey> : IPlatformRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
{
    public Task<TEntity> CreateAsync(TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     CreateOrUpdateAsync
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="dismissSendEvent"></param>
    /// <param name="eventCustomConfig"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<TEntity> CreateOrUpdateAsync(TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     CreateOrUpdateManyAsync
    /// </summary>
    /// <param name="entities"></param>
    /// <param name="dismissSendEvent"></param>
    /// <param name="customCheckExistingPredicateBuilder">
    ///     Used to help to check entity existence to update by other props than
    ///     default using Id. Example: toUpsertEntity => p => p.Name == toUpsertEntity.Name
    /// </param>
    /// <param name="eventCustomConfig"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<List<TEntity>> CreateOrUpdateManyAsync(List<TEntity> entities,
        bool dismissSendEvent = false,
        Func<TEntity, Expression<Func<TEntity, bool>>> customCheckExistingPredicateBuilder = null,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> CreateOrUpdateManyAsync(IUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Func<TEntity, Expression<Func<TEntity, bool>>> customCheckExistingPredicateBuilder = null,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<TEntity> UpdateAsync(TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<TEntity> DeleteAsync(TPrimaryKey entityId,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<TEntity> DeleteAsync(TEntity entity,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> CreateManyAsync(List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> CreateManyAsync(IUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> UpdateManyAsync(List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> UpdateManyAsync(IUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> UpdateManyAsync(Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> updateAction,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> DeleteManyAsync(List<TPrimaryKey> entityIds,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> DeleteManyAsync(List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> DeleteManyAsync(IUnitOfWork uow,
        List<TEntity> entities,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<List<TEntity>> DeleteManyAsync(Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public Task<TEntity> CreateOrUpdateAsync(TEntity entity,
        Expression<Func<TEntity, bool>> customCheckExistingPredicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default);

    public async Task<TEntity> CreateIfNotExistAsync(TEntity entity,
        Expression<Func<TEntity, bool>> predicate,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        var isExisted = await AnyAsync(predicate, cancellationToken);
        if (isExisted) return entity;

        return await CreateAsync(entity, dismissSendEvent, eventCustomConfig,
            cancellationToken);
    }
}

public interface IPlatformQueryableRepository<TEntity, TPrimaryKey> : IPlatformRepository<TEntity, TPrimaryKey>
    where TEntity : class, IEntity<TPrimaryKey>, new()
{
    /// <summary>
    ///     Return enumerable from query belong to <see cref="IUnitOfWorkManager.GlobalUow" />. <br />
    ///     A single separated global uow in current scoped is used by repository for read data using query, usually when need
    ///     to return data
    ///     as enumerable to help download data like streaming data (not load all big data into ram) <br />
    ///     or any other purpose that just want to using query directly without affecting the current active uow. <br />
    ///     This uow is auto created once per scope when access it. <br />
    ///     This won't affect the normal current uow queue list when Begin a new uow.
    /// </summary>
    public IQueryable<TEntity> GetGlobalUowQuery(params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    /// <summary>
    ///     Return query of <see cref="IUnitOfWorkManager.CurrentActiveUow" />
    /// </summary>
    public IQueryable<TEntity> GetCurrentUowQuery(params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    /// <see cref="GetCurrentUowQuery" />
    public IQueryable<TResult> GetCurrentUowQuery<TResult>(Func<IQueryable<TEntity>, IQueryable<TResult>> queryBuilder,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        return queryBuilder(GetCurrentUowQuery(loadRelatedEntities));
    }

    /// <summary>
    ///     Build and get query from a given uow
    /// </summary>
    public IQueryable<TResult> GetQuery<TResult>(IUnitOfWork uow,
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> queryBuilder,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities)
    {
        return queryBuilder(uow, GetQuery(uow, loadRelatedEntities));
    }

    /// <summary>
    ///     Get query from a given uow
    /// </summary>
    public IQueryable<TEntity> GetQuery(IUnitOfWork uow,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<List<TEntity>> GetAllAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<List<TEntity>> GetAllAsync(Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<List<TEntity>> GetAllAsync(IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public IAsyncEnumerable<TEntity> GetAllAsyncEnumerable(IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public Task<TEntity> FirstOrDefaultAsync(IQueryable<TEntity> query,
        CancellationToken cancellationToken = default);

    public Task<TEntity> FirstOrDefaultAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<TEntity> FirstOrDefaultAsync(Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TEntity>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IQueryable<TEntity>, IEnumerable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IEnumerable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<List<TSelector>> GetAllAsync<TSelector>(Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<List<TSelector>> GetAllAsync<TSelector>(Func<IQueryable<TEntity>, IEnumerable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<List<TSelector>> GetAllAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IEnumerable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public IAsyncEnumerable<TSelector> GetAllAsyncEnumerable<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<TSelector> FirstOrDefaultAsync<TSelector>(Func<IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<TSelector> FirstOrDefaultAsync<TSelector>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TSelector>> queryBuilder,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] loadRelatedEntities);

    public Task<int> CountAsync<TQueryItemResult>(Func<IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default);

    public Task<int> CountAsync<TQueryItemResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TQueryItemResult>> queryBuilder,
        CancellationToken cancellationToken = default);

    public Task<int> CountAsync(IQueryable<TEntity> query, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Help to create a function to return a query which use want to use in the queryBuilder for a lot of
    ///     other function. It's important to support PARALLELS query. If use GetAllQuery() to use outside, we need to open a
    ///     uow without close,
    ///     which could not run parallels because db context is not thread safe. <br />
    ///     Ex:
    ///     <br />
    ///     var fullItemsQueryBuilder = repository.GetQueryBuilder(query => query.Where());<br />
    ///     var pagedEntities = await repository.GetAllAsync(queryBuilder: query =>
    ///     fullItemsQueryBuilder(query).PageBy(request.SkipCount, request.MaxResultCount));<br />
    ///     var totalCount = await repository.CountAsync(fullItemsQueryBuilder, cancellationToken);
    /// </summary>
    public Func<IQueryable<TEntity>, IQueryable<TResult>> GetQueryBuilder<TResult>(
        Func<IQueryable<TEntity>, IQueryable<TResult>> builderFn);

    /// <summary>
    ///     Help to create a function to return a query which use want to use in the queryBuilder for a lot of
    ///     other function. It's important to support PARALLELS query. If use GetAllQuery() to use outside, we need to open a
    ///     uow without close,
    ///     which could not run parallels because db context is not thread safe. <br />
    ///     Ex:
    ///     <br />
    ///     var fullItemsQueryBuilder = repository.GetQueryBuilder((uow, query) => query.Where());<br />
    ///     var pagedEntities = await repository.GetAllAsync(queryBuilder: (uow, query) =>
    ///     fullItemsQueryBuilder(query).PageBy(request.SkipCount, request.MaxResultCount));<br />
    ///     var totalCount = await repository.CountAsync(fullItemsQueryBuilder, cancellationToken);
    /// </summary>
    public Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> GetQueryBuilder<TResult>(
        Func<IUnitOfWork, IQueryable<TEntity>, IQueryable<TResult>> builderFn);

    /// <summary>
    ///     Help to create a function to return a query which use want to use in the queryBuilder for a lot of
    ///     other function. It's important to support PARALLELS query. If use GetAllQuery() to use outside, we need to open a
    ///     uow without close,
    ///     which could not run parallels because db context is not thread safe. <br />
    ///     Ex:
    ///     <br />
    ///     var fullItemsQueryBuilder = repository.GetQueryBuilder(p => p.PropertyX == true);<br />
    ///     var pagedEntities = await repository.GetAllAsync(queryBuilder: query =>
    ///     fullItemsQueryBuilder(query).PageBy(request.SkipCount, request.MaxResultCount));<br />
    ///     var totalCount = await repository.CountAsync(fullItemsQueryBuilder, cancellationToken);
    /// </summary>
    public Func<IQueryable<TEntity>, IQueryable<TEntity>> GetQueryBuilder(Expression<Func<TEntity, bool>> predicate);

    public Expression<Func<TEntity, bool>> GetQueryExpr(Expression<Func<TEntity, bool>> predicate)
    {
        return predicate;
    }
}

public interface IPlatformQueryableRootRepository<TEntity, TPrimaryKey>
    : IPlatformQueryableRepository<TEntity, TPrimaryKey>, IPlatformRootRepository<TEntity, TPrimaryKey>
    where TEntity : class, IRootEntity<TPrimaryKey>, new()
{
    public async Task DeleteManyScrollingPagingAsync(Expression<Func<TEntity, bool>> predicate,
        int pageSize,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        await Util.Pager.ExecuteScrollingPagingAsync(async () =>
            {
                using (var uow = UowManager().CreateNewUow())
                {
                    var pagingDeleteItems = await GetAllAsync(GetQuery(uow).Where(predicate).Take(pageSize),
                        cancellationToken);

                    await DeleteManyAsync(uow, pagingDeleteItems, dismissSendEvent,
                        eventCustomConfig, cancellationToken);

                    await uow.CompleteAsync(cancellationToken);

                    return pagingDeleteItems;
                }
            },
            await CountAsync(predicate, cancellationToken).Then(totalItemsCount => totalItemsCount / pageSize));
    }

    /// <summary>
    ///     Use paging when every time to update will NOT decrease the total items get from the "predicate"
    /// </summary>
    public async Task UpdateManyPagingAsync(Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> updateAction,
        int pageSize,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        await Util.Pager.ExecutePagingAsync(async (skipCount, pageSize) =>
            {
                using (var uow = UowManager().CreateNewUow())
                {
                    var pagingUpdateItems = await GetAllAsync(
                            GetQuery(uow).Where(predicate).Skip(skipCount).Take(pageSize),
                            cancellationToken)
                        .ThenAction(items => items.ForEach(updateAction));

                    await UpdateManyAsync(uow, pagingUpdateItems, dismissSendEvent,
                        eventCustomConfig, cancellationToken);

                    await uow.CompleteAsync(cancellationToken);
                }
            },
            await CountAsync(predicate, cancellationToken),
            pageSize);
    }

    /// <summary>
    ///     Use scrolling paging when every time to update will decrease the total items get from the "predicate"
    /// </summary>
    public async Task UpdateManyScrollingPagingAsync(Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> updateAction,
        int pageSize,
        bool dismissSendEvent = false,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
    {
        await Util.Pager.ExecuteScrollingPagingAsync(async () =>
            {
                using (var uow = UowManager().CreateNewUow())
                {
                    var pagingUpdateItems = await GetAllAsync(GetQuery(uow).Where(predicate).Take(pageSize),
                            cancellationToken)
                        .ThenAction(items => items.ForEach(updateAction));

                    var updatedItems = await UpdateManyAsync(uow, pagingUpdateItems, dismissSendEvent,
                        eventCustomConfig, cancellationToken);

                    return updatedItems;
                }
            },
            await CountAsync(predicate, cancellationToken).Then(totalItemsCount => totalItemsCount / pageSize));
    }
}