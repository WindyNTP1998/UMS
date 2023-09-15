using UMS.Platform.Common.Cqrs;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Domain.Events;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Domain.Services;

/// <summary>
///     Domain service is used to serve business logic operation related to many root domain entities,
///     the business logic term is understood by domain expert.
/// </summary>
public interface IPlatformDomainService
{
}

public abstract class PlatformDomainService : IPlatformDomainService
{
    protected readonly IPlatformCqrs Cqrs;
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformDomainService(IPlatformCqrs cqrs,
        IUnitOfWorkManager unitOfWorkManager)
    {
        Cqrs = cqrs;
        UnitOfWorkManager = unitOfWorkManager;
    }

    protected Task SendEvent<TEvent>(TEvent domainEvent, CancellationToken token = default)
        where TEvent : PlatformCqrsDomainEvent
    {
        return Cqrs.SendEvent(domainEvent.With(_ => _.SourceUowId = UnitOfWorkManager.TryGetCurrentActiveUow()?.Id),
            token);
    }
}