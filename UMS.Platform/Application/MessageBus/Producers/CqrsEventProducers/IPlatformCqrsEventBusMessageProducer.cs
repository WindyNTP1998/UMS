using MediatR;
using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context;
using UMS.Platform.Application.Context.UserContext;
using UMS.Platform.Application.Cqrs.Events;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs.Events;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Domain.Events;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.Producers.CqrsEventProducers;

public interface IPlatformCqrsEventBusMessageProducer
{
    public static Type GetTMessageArgumentType(Type cqrsEventBusMessageProducerType)
    {
        return cqrsEventBusMessageProducerType.GetGenericArguments()[1];
    }
}

/// <summary>
///     This interface is used for conventional register all PlatformCqrsEventBusProducer
/// </summary>
public interface IPlatformCqrsEventBusMessageProducer<in TEvent>
    : INotificationHandler<TEvent>, IPlatformCqrsEventBusMessageProducer
    where TEvent : PlatformCqrsEvent, new()
{
}

public abstract class PlatformCqrsEventBusMessageProducer<TEvent, TMessage>
    : PlatformCqrsEventApplicationHandler<TEvent>, IPlatformCqrsEventBusMessageProducer<TEvent>
    where TEvent : PlatformCqrsEvent, new()
    where TMessage : class, new()
{
    protected readonly IPlatformApplicationBusMessageProducer ApplicationBusMessageProducer;
    protected readonly IPlatformApplicationUserContextAccessor UserContext;

    public PlatformCqrsEventBusMessageProducer(ILoggerFactory loggerFactory,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformApplicationBusMessageProducer applicationBusMessageProducer,
        IPlatformApplicationUserContextAccessor userContextAccessor,
        IPlatformApplicationSettingContext applicationSettingContext) : base(loggerFactory, unitOfWorkManager,
        serviceProvider, rootServiceProvider)
    {
        ApplicationBusMessageProducer = applicationBusMessageProducer;
        ApplicationSettingContext = applicationSettingContext;
        UserContext = userContextAccessor;
    }

    public override bool EnableInboxEventBusMessage => false;

    protected override bool AutoOpenUow => false;

    protected override bool AllowUsingUserContextAccessor => true;

    protected IPlatformApplicationSettingContext ApplicationSettingContext { get; }

    protected abstract TMessage BuildMessage(TEvent @event);

    protected override async Task HandleAsync(TEvent @event,
        CancellationToken cancellationToken)
    {
        await SendMessage(@event, cancellationToken);
    }

    public override void LogError(TEvent notification, Exception exception, ILoggerFactory loggerFactory)
    {
        CreateLogger(loggerFactory)
            .LogError(exception,
                "[PlatformCqrsEventBusMessageProducer] Failed to send {MessageName}. [[Error:{Error}]]. MessageContent: {MessageContent}.",
                typeof(TMessage).FullName,
                exception.Message,
                exception.As<PlatformMessageBusException<TMessage>>()?.EventBusMessage.ToJson());
    }

    protected virtual async Task SendMessage(TEvent @event, CancellationToken cancellationToken)
    {
        await ApplicationBusMessageProducer.SendAsync(BuildMessage(@event),
            forceUseDefaultRoutingKey: !SendByMessageSelfRoutingKey(),
            sourceOutboxUowId: @event.As<IPlatformUowEvent>()?.SourceUowId,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     Default is False. If True, send message using RoutingKey() from <see cref="IPlatformSelfRoutingKeyBusMessage" />
    /// </summary>
    /// <returns></returns>
    protected virtual bool SendByMessageSelfRoutingKey()
    {
        return false;
    }

    protected PlatformBusMessageIdentity BuildPlatformEventBusMessageIdentity()
    {
        return new PlatformBusMessageIdentity
        {
            UserId = UserContextAccessor.Current.UserId(),
            RequestId = UserContextAccessor.Current.RequestId(),
            UserName = UserContextAccessor.Current.UserName()
        };
    }
}