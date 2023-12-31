using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context;
using UMS.Platform.Application.MessageBus.Consumers;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Hosting;
using UMS.Platform.Common.JsonSerialization;
using UMS.Platform.Common.Timing;
using UMS.Platform.Common.Utils;
using UMS.Platform.Domain.Exceptions;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.InboxPattern;

/// <summary>
///     Run in an interval to scan messages in InboxCollection in database, check new message to consume it
/// </summary>
public class PlatformConsumeInboxBusMessageHostedService : PlatformIntervalProcessHostedService
{
    public const int MinimumRetryConsumeInboxMessageTimesToWarning = 3;

    private readonly IPlatformApplicationSettingContext applicationSettingContext;
    private readonly PlatformInboxConfig inboxConfig;
    private readonly PlatformMessageBusConfig messageBusConfig;

    private bool isProcessing;

    public PlatformConsumeInboxBusMessageHostedService(IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        IPlatformMessageBusScanner messageBusScanner,
        PlatformInboxConfig inboxConfig,
        PlatformMessageBusConfig messageBusConfig) : base(serviceProvider, loggerFactory)
    {
        this.applicationSettingContext = applicationSettingContext;
        this.inboxConfig = inboxConfig;
        this.messageBusConfig = messageBusConfig;
        ConsumerByNameToTypeDic = messageBusScanner
            .ScanAllDefinedConsumerTypes()
            .ToDictionary(PlatformInboxBusMessage.GetConsumerByValue);
        InvokeConsumerLogger = loggerFactory.CreateLogger(typeof(PlatformMessageBusConsumer));
    }

    protected Dictionary<string, Type> ConsumerByNameToTypeDic { get; }

    protected ILogger InvokeConsumerLogger { get; }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        if (!HasInboxEventBusMessageRepositoryRegistered() || isProcessing) return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the database is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => ConsumeInboxEventBusMessages(cancellationToken),
                retryAttempt => 10.Seconds(),
                inboxConfig.ProcessConsumeMessageRetryCount,
                onRetry: (ex, timeSpan, currentRetry,
                    ctx) =>
                {
                    if (currentRetry >= MinimumRetryConsumeInboxMessageTimesToWarning)
                        Logger.LogWarning(ex,
                            "Retry ConsumeInboxEventBusMessages {CurrentRetry} time(s) failed. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                            currentRetry,
                            applicationSettingContext.ApplicationName,
                            applicationSettingContext.ApplicationAssembly.FullName);
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Retry ConsumeInboxEventBusMessages failed. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                applicationSettingContext.ApplicationName,
                applicationSettingContext.ApplicationAssembly.FullName);
        }

        isProcessing = false;
    }

    protected virtual async Task ConsumeInboxEventBusMessages(CancellationToken cancellationToken)
    {
        do
        {
            var toHandleMessages = await PopToHandleInboxEventBusMessages(cancellationToken);

            // Group by ConsumerBy to handling multiple different consumers parallel
            await toHandleMessages
                .GroupBy(p => p.ConsumerBy)
                .ParallelAsync(async consumerMessages =>
                {
                    // Message in the same consumer queue but created on the same seconds usually from different data/users and not dependent,
                    // so that we could process it in parallel
                    await consumerMessages
                        .GroupBy(p => p.CreatedDate.AddMilliseconds(-p.CreatedDate.Millisecond))
                        .ForEachAsync(groupSameTimeSeconds =>
                            groupSameTimeSeconds.ParallelAsync(HandleInboxMessageAsync));
                });

            // Random wait to decrease the chance that multiple deploy instance could process same messages at the same time
            await Task.Delay(Util.RandomGenerator.Next(0, 10000).Milliseconds(), cancellationToken);
        } while (await IsAnyMessagesToHandleAsync());

        async Task HandleInboxMessageAsync(PlatformInboxBusMessage toHandleInboxMessage)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                try
                {
                    await InvokeConsumerAsync(scope,
                        toHandleInboxMessage,
                        cancellationToken);
                }
                catch (Exception e)
                {
                    Logger.LogError(e,
                        "[PlatformConsumeInboxEventBusMessageHostedService] Try to consume inbox message with Id:{MessageId} failed. Message Content:{InboxMessage}",
                        toHandleInboxMessage.Id,
                        toHandleInboxMessage.ToJson());
                }
            }
        }
    }

    protected Task<bool> IsAnyMessagesToHandleAsync()
    {
        return ServiceProvider.ExecuteInjectScopedAsync<bool>(
            (IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
            {
                return inboxEventBusMessageRepo!.AnyAsync(
                    PlatformInboxBusMessage.CanHandleMessagesExpr(inboxConfig.MessageProcessingMaxSeconds));
            });
    }

    protected virtual async Task InvokeConsumerAsync(IServiceScope scope,
        PlatformInboxBusMessage toHandleInboxMessage,
        CancellationToken cancellationToken)
    {
        var consumerType = ResolveConsumerType(toHandleInboxMessage);

        if (consumerType != null)
        {
            var consumer = scope.ServiceProvider.GetService(consumerType)
                .As<IPlatformApplicationMessageBusConsumer>()
                .With(_ => _.HandleDirectlyExistingInboxMessage = toHandleInboxMessage);

            var consumerMessageType = PlatformMessageBusConsumer.GetConsumerMessageType(consumer);

            var busMessage = Util.TaskRunner.CatchExceptionContinueThrow(() => PlatformJsonSerializer.Deserialize(
                    toHandleInboxMessage.JsonMessage,
                    consumerMessageType,
                    consumer.CustomJsonSerializerOptions()),
                ex => Logger.LogError(ex,
                    "RabbitMQ parsing message to {ConsumerMessageType.Name}. [[Error:{Error}]]. Body: {InboxMessage}",
                    consumerMessageType.Name,
                    ex.Message,
                    toHandleInboxMessage.JsonMessage));

            if (busMessage != null)
                await PlatformMessageBusConsumer.InvokeConsumerAsync(consumer,
                    busMessage,
                    toHandleInboxMessage.RoutingKey,
                    messageBusConfig,
                    InvokeConsumerLogger);
        }
        else
        {
            await PlatformInboxMessageBusConsumerHelper.UpdateExistingInboxFailedMessageAsync(scope.ServiceProvider,
                toHandleInboxMessage.Id,
                new Exception(
                    $"[{GetType().Name}] Error resolve consumer type {toHandleInboxMessage.ConsumerBy}. InboxId:{toHandleInboxMessage.Id} "),
                inboxConfig.RetryProcessFailedMessageInSecondsUnit,
                () => Logger,
                cancellationToken);
        }
    }

    protected async Task<List<PlatformInboxBusMessage>> PopToHandleInboxEventBusMessages(
        CancellationToken cancellationToken)
    {
        try
        {
            return await ServiceProvider.ExecuteInjectScopedAsync<List<PlatformInboxBusMessage>>(
                async (IUnitOfWorkManager uowManager, IPlatformInboxBusMessageRepository inboxEventBusMessageRepo) =>
                {
                    using (var uow = uowManager!.Begin())
                    {
                        var toHandleMessages = await inboxEventBusMessageRepo.GetAllAsync(query => query
                                .Where(PlatformInboxBusMessage.CanHandleMessagesExpr(inboxConfig
                                    .MessageProcessingMaxSeconds))
                                .OrderBy(p => p.LastConsumeDate)
                                .Take(inboxConfig.NumberOfProcessConsumeInboxMessagesBatch),
                            cancellationToken);

                        if (toHandleMessages.IsEmpty()) return toHandleMessages;

                        toHandleMessages.ForEach(p =>
                        {
                            p.ConsumeStatus = PlatformInboxBusMessage.ConsumeStatuses.Processing;
                            p.LastConsumeDate = Clock.UtcNow;
                        });

                        await inboxEventBusMessageRepo.UpdateManyAsync(toHandleMessages,
                            true,
                            null,
                            cancellationToken);

                        await uow.CompleteAsync(cancellationToken);

                        return toHandleMessages;
                    }
                });
        }
        catch (PlatformDomainRowVersionConflictException conflictDomainException)
        {
            Logger.LogWarning(conflictDomainException,
                "Some other consumer instance has been handling some inbox messages (support multi service instance running concurrently), which lead to row version conflict. This is as expected so just warning.");

            // WHY: Because support multi service instance running concurrently,
            // get row version conflict is expected, so just retry again to get unprocessed inbox messages
            return await PopToHandleInboxEventBusMessages(cancellationToken);
        }
    }

    protected bool HasInboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope =>
            scope.ServiceProvider.GetService<IPlatformInboxBusMessageRepository>() != null);
    }

    private Type ResolveConsumerType(PlatformInboxBusMessage toHandleInboxMessage)
    {
        return ConsumerByNameToTypeDic.GetValueOrDefault(toHandleInboxMessage.ConsumerBy, null);
    }
}