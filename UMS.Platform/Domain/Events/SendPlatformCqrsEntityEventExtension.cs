using UMS.Platform.Common.Cqrs;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Domain.Entities;

namespace UMS.Platform.Domain.Events;

public static class SendPlatformCqrsEntityEventExtension
{
    public static async Task SendEntityEvent<TEntity>(this IPlatformCqrs cqrs,
        TEntity entity,
        PlatformCqrsEntityEventCrudAction crudAction,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity, new()
    {
        await cqrs.SendEvent(
            new PlatformCqrsEntityEvent<TEntity>(entity, crudAction).With(_ => eventCustomConfig?.Invoke(_)),
            cancellationToken);
    }

    public static async Task SendEntityEvents<TEntity>(this IPlatformCqrs cqrs,
        IList<TEntity> entities,
        PlatformCqrsEntityEventCrudAction crudAction,
        Action<PlatformCqrsEntityEvent> eventCustomConfig = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IEntity, new()
    {
        await cqrs.SendEvents(
            entities.SelectList(entity =>
                new PlatformCqrsEntityEvent<TEntity>(entity, crudAction).With(_ => eventCustomConfig?.Invoke(_))),
            cancellationToken);
    }
}