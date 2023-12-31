using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context.UserContext;
using UMS.Platform.Application.MessageBus.InboxPattern;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs.Events;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.Consumers;

public interface IPlatformApplicationMessageBusConsumer : IPlatformMessageBusConsumer
{
    public PlatformInboxBusMessage HandleDirectlyExistingInboxMessage { get; set; }

    public bool AutoDeleteProcessedInboxEventMessage { get; set; }

    public bool IsInstanceExecutingFromInboxHelper { get; set; }

    public bool AllowProcessInboxMessageInBackgroundThread { get; set; }
}

public interface IPlatformApplicationMessageBusConsumer<in TMessage>
    : IPlatformMessageBusConsumer<TMessage>, IPlatformApplicationMessageBusConsumer
    where TMessage : class, new()
{
}

public abstract class PlatformApplicationMessageBusConsumer<TMessage>
    : PlatformMessageBusConsumer<TMessage>, IPlatformApplicationMessageBusConsumer<TMessage>
    where TMessage : class, new()
{
    protected readonly IPlatformInboxBusMessageRepository InboxBusMessageRepo;
    protected readonly PlatformInboxConfig InboxConfig;
    protected readonly IPlatformRootServiceProvider RootServiceProvider;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly IUnitOfWorkManager UowManager;

    protected PlatformApplicationMessageBusConsumer(ILoggerFactory loggerFactory,
        IUnitOfWorkManager uowManager,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory)
    {
        UowManager = uowManager;
        InboxBusMessageRepo = serviceProvider.GetService<IPlatformInboxBusMessageRepository>();
        InboxConfig = serviceProvider.GetRequiredService<PlatformInboxConfig>();
        ServiceProvider = serviceProvider;
        RootServiceProvider = rootServiceProvider;

        IsInjectingUserContextAccessor =
            GetType().IsUsingGivenTypeViaConstructor<IPlatformApplicationUserContextAccessor>();
        if (IsInjectingUserContextAccessor)
            CreateLogger(loggerFactory)
                .LogError(
                    "{EventHandlerType} is injecting and using {IPlatformApplicationUserContextAccessor}, which will make the event handler could not run in background thread. " +
                    "The event sender must wait the handler to be finished. Should use the {RequestContext} info in the event instead.",
                    GetType().Name,
                    nameof(IPlatformApplicationUserContextAccessor),
                    nameof(PlatformCqrsEvent.RequestContext));
    }

    public virtual bool AutoBeginUow => true;
    public bool IsInjectingUserContextAccessor { get; set; }

    protected IPlatformApplicationUserContextAccessor UserContextAccessor =>
        ServiceProvider.GetRequiredService<IPlatformApplicationUserContextAccessor>();

    public PlatformInboxBusMessage HandleDirectlyExistingInboxMessage { get; set; }
    public bool AutoDeleteProcessedInboxEventMessage { get; set; }
    public bool IsInstanceExecutingFromInboxHelper { get; set; }
    public bool AllowProcessInboxMessageInBackgroundThread { get; set; }

    protected override async Task ExecuteHandleLogicAsync(TMessage message, string routingKey)
    {
        if (message is IPlatformTrackableBusMessage trackableBusMessage)
            UserContextAccessor.Current.UpsertMany(trackableBusMessage.RequestContext);

        if (InboxBusMessageRepo != null && !IsInstanceExecutingFromInboxHelper)
        {
            await PlatformInboxMessageBusConsumerHelper.HandleExecutingInboxConsumerAsync(RootServiceProvider,
                ServiceProvider,
                this,
                InboxBusMessageRepo,
                InboxConfig,
                message,
                routingKey,
                CreateGlobalLogger,
                InboxConfig.RetryProcessFailedMessageInSecondsUnit,
                AllowProcessInboxMessageInBackgroundThread,
                HandleDirectlyExistingInboxMessage,
                autoDeleteProcessedMessage: AutoDeleteProcessedInboxEventMessage,
                handleInUow: null);
        }
        else
        {
            if (AutoBeginUow)
                using (var uow = UowManager.Begin())
                {
                    await HandleLogicAsync(message, routingKey);
                    await uow.CompleteAsync();
                }
            else
                await HandleLogicAsync(message, routingKey);
        }
    }

    public ILogger CreateGlobalLogger()
    {
        return CreateLogger(RootServiceProvider.GetRequiredService<ILoggerFactory>());
    }
}