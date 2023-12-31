using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMS.Platform.Common;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.JsonSerialization;
using UMS.Platform.Common.Utils;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxMessageBusProducerHelper : IPlatformHelper
{
    public const int DefaultResilientRetiredCount = 2;
    public const int DefaultResilientRetiredDelayMilliseconds = 200;

    private readonly IPlatformMessageBusProducer messageBusProducer;
    private readonly PlatformOutboxConfig outboxConfig;
    private readonly IPlatformRootServiceProvider rootServiceProvider;
    private readonly IServiceProvider serviceProvider;

    public PlatformOutboxMessageBusProducerHelper(PlatformOutboxConfig outboxConfig,
        IPlatformMessageBusProducer messageBusProducer,
        IServiceProvider serviceProvider,
        IPlatformRootServiceProvider rootServiceProvider)
    {
        this.outboxConfig = outboxConfig;
        this.messageBusProducer = messageBusProducer;
        this.serviceProvider = serviceProvider;
        this.rootServiceProvider = rootServiceProvider;
    }

    public async Task HandleSendingOutboxMessageAsync<TMessage>(TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        PlatformOutboxBusMessage handleExistingOutboxMessage = null,
        string sourceOutboxUowId = null,
        CancellationToken cancellationToken = default) where TMessage : class, new()
    {
        if (serviceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null)
        {
            if (outboxConfig.StandaloneScopeForOutbox)
                await serviceProvider.ExecuteInjectScopedAsync((
                    IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
                    IPlatformMessageBusProducer messageBusProducer,
                    IUnitOfWorkManager unitOfWorkManager) => SendOutboxMessageAsync(message,
                    routingKey,
                    retryProcessFailedMessageInSecondsUnit,
                    handleExistingOutboxMessage,
                    sourceOutboxUowId,
                    cancellationToken,
                    CreateLogger(),
                    outboxBusMessageRepository,
                    messageBusProducer,
                    unitOfWorkManager));
            else
                await serviceProvider.ExecuteInjectAsync((
                    IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
                    IPlatformMessageBusProducer messageBusProducer,
                    IUnitOfWorkManager unitOfWorkManager) => SendOutboxMessageAsync(message,
                    routingKey,
                    retryProcessFailedMessageInSecondsUnit,
                    handleExistingOutboxMessage,
                    sourceOutboxUowId,
                    cancellationToken,
                    CreateLogger(),
                    outboxBusMessageRepository,
                    messageBusProducer,
                    unitOfWorkManager));
        }
        else
        {
            await messageBusProducer.SendAsync(message, routingKey, cancellationToken);
        }
    }

    public async Task SendOutboxMessageAsync<TMessage>(TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        PlatformOutboxBusMessage handleExistingOutboxMessage,
        string sourceOutboxUowId,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository,
        IPlatformMessageBusProducer messageBusProducer,
        IUnitOfWorkManager unitOfWorkManager) where TMessage : class, new()
    {
        if (handleExistingOutboxMessage != null &&
            PlatformOutboxBusMessage.CanHandleMessagesExpr(outboxConfig.MessageProcessingMaxSeconds).Compile()(
                handleExistingOutboxMessage))
            await SendExistingOutboxMessageAsync(handleExistingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken,
                logger,
                messageBusProducer,
                outboxBusMessageRepository);
        else if (handleExistingOutboxMessage == null)
            await SaveAndTrySendNewOutboxMessageAsync(message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                sourceOutboxUowId,
                cancellationToken,
                logger,
                unitOfWorkManager,
                outboxBusMessageRepository);
    }

    public async Task SendExistingOutboxMessageAsync<TMessage>(PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformMessageBusProducer messageBusProducer,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
        where TMessage : class, new()
    {
        try
        {
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(async () =>
                {
                    await messageBusProducer.SendAsync(message, routingKey, cancellationToken);

                    await UpdateExistingOutboxMessageProcessedAsync(existingOutboxMessage,
                        cancellationToken,
                        outboxBusMessageRepository);
                },
                retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
                DefaultResilientRetiredCount);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SendExistingOutboxMessageAsync failed. [[Error:{Error}]]", exception.Message);

            await UpdateExistingOutboxMessageFailedInNewScopeAsync(existingOutboxMessage, exception,
                retryProcessFailedMessageInSecondsUnit, cancellationToken, logger);
        }
    }

    public async Task SendExistingOutboxMessageInNewUowAsync<TMessage>(PlatformOutboxBusMessage existingOutboxMessage,
        TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider)
        where TMessage : class, new()
    {
        try
        {
            await serviceProvider.ExecuteInjectAsync(SendExistingOutboxMessageAsync<TMessage>,
                existingOutboxMessage,
                message,
                routingKey,
                retryProcessFailedMessageInSecondsUnit,
                cancellationToken,
                logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SendExistingOutboxMessageInNewUowAsync failed. [[Error:{Error}]]",
                exception.Message);

            await UpdateExistingOutboxMessageFailedInNewScopeAsync(existingOutboxMessage, exception,
                retryProcessFailedMessageInSecondsUnit, cancellationToken, logger);
        }
    }

    public static async Task UpdateExistingOutboxMessageProcessedAsync(PlatformOutboxBusMessage existingOutboxMessage,
        CancellationToken cancellationToken,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(async () =>
            {
                existingOutboxMessage.LastSendDate = DateTime.UtcNow;
                existingOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Processed;

                await outboxBusMessageRepository.UpdateAsync(existingOutboxMessage, true, null,
                    cancellationToken);
            },
            retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
            DefaultResilientRetiredCount);
    }

    public async Task UpdateExistingOutboxMessageFailedInNewScopeAsync(PlatformOutboxBusMessage existingOutboxMessage,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        try
        {
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(async () =>
                {
                    await rootServiceProvider.ExecuteInjectScopedAsync(
                        async (IPlatformOutboxBusMessageRepository outboxBusMessageRepository) =>
                        {
                            var latestCurrentExistingOutboxMessage =
                                await outboxBusMessageRepository.FirstOrDefaultAsync(p =>
                                    p.Id == existingOutboxMessage.Id);

                            if (latestCurrentExistingOutboxMessage != null)
                                await UpdateExistingOutboxMessageFailedAsync(latestCurrentExistingOutboxMessage,
                                    exception,
                                    retryProcessFailedMessageInSecondsUnit,
                                    cancellationToken,
                                    logger,
                                    outboxBusMessageRepository);
                        });
                },
                retryCount: DefaultResilientRetiredCount,
                sleepDurationProvider: retryAttempt => DefaultResilientRetiredDelayMilliseconds.Milliseconds());
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "UpdateExistingOutboxMessageFailedInNewUowAsync failed. [[Error:{Error}]]].",
                ex.Message);
        }
    }

    private static async Task UpdateExistingOutboxMessageFailedAsync(PlatformOutboxBusMessage existingOutboxMessage,
        Exception exception,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken,
        ILogger logger,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
    {
        existingOutboxMessage.SendStatus = PlatformOutboxBusMessage.SendStatuses.Failed;
        existingOutboxMessage.LastSendDate = DateTime.UtcNow;
        existingOutboxMessage.LastSendError = PlatformJsonSerializer.Serialize(new
        {
            exception.Message,
            exception.StackTrace
        });
        existingOutboxMessage.RetriedProcessCount = (existingOutboxMessage.RetriedProcessCount ?? 0) + 1;
        existingOutboxMessage.NextRetryProcessAfter = PlatformOutboxBusMessage.CalculateNextRetryProcessAfter(
            existingOutboxMessage.RetriedProcessCount,
            retryProcessFailedMessageInSecondsUnit);

        await outboxBusMessageRepository.CreateOrUpdateAsync(existingOutboxMessage, true, null,
            cancellationToken);

        LogSendOutboxMessageFailed(exception, existingOutboxMessage, logger);
    }

    protected async Task SaveAndTrySendNewOutboxMessageAsync<TMessage>(TMessage message,
        string routingKey,
        double retryProcessFailedMessageInSecondsUnit,
        string sourceOutboxUowId,
        CancellationToken cancellationToken,
        ILogger logger,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformOutboxBusMessageRepository outboxBusMessageRepository)
        where TMessage : class, new()
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(async () =>
            {
                var messageTrackId = message.As<IPlatformTrackableBusMessage>()?.TrackingId;

                var existedOutboxMessage = messageTrackId != null
                    ? await outboxBusMessageRepository.FirstOrDefaultAsync(
                        p => p.Id == PlatformOutboxBusMessage.BuildId(messageTrackId),
                        cancellationToken)
                    : null;

                var newOutboxMessage = existedOutboxMessage == null
                    ? await outboxBusMessageRepository.CreateAsync(PlatformOutboxBusMessage.Create(message,
                            messageTrackId,
                            routingKey,
                            PlatformOutboxBusMessage.SendStatuses.Processing),
                        true,
                        null,
                        cancellationToken)
                    : null;

                var toProcessInboxMessage = existedOutboxMessage ?? newOutboxMessage;

                if (existedOutboxMessage == null ||
                    PlatformOutboxBusMessage.CanHandleMessagesExpr(outboxConfig.MessageProcessingMaxSeconds).Compile()(
                        existedOutboxMessage))
                {
                    var currentActiveUow = sourceOutboxUowId != null
                        ? unitOfWorkManager.TryGetCurrentOrCreatedActiveUow(sourceOutboxUowId)
                        : null;
                    // WHY: Do not need to wait for uow completed if the uow for db do not handle actually transaction.
                    // Can execute it immediately without waiting for uow to complete
                    if (currentActiveUow == null || currentActiveUow.IsPseudoTransactionUow())
                        Util.TaskRunner.QueueActionInBackground(async () =>
                                await rootServiceProvider.ExecuteInjectScopedAsync(
                                    SendExistingOutboxMessageAsync<TMessage>,
                                    toProcessInboxMessage,
                                    message,
                                    routingKey,
                                    retryProcessFailedMessageInSecondsUnit,
                                    cancellationToken,
                                    logger),
                            CreateLogger,
                            cancellationToken: cancellationToken);
                    else
                        currentActiveUow.OnCompletedActions.Add(async () =>
                        {
                            // Try to process sending newProcessingOutboxMessage first time immediately after task completed
                            // WHY: we can wait for the background process handle the message but try to do it
                            // immediately if possible is better instead of waiting for the background process
                            await rootServiceProvider.ExecuteInjectScopedAsync(
                                SendExistingOutboxMessageInNewUowAsync<TMessage>,
                                toProcessInboxMessage,
                                message,
                                routingKey,
                                retryProcessFailedMessageInSecondsUnit,
                                cancellationToken,
                                logger);
                        });
                }
            },
            retryAttempt => (retryAttempt * DefaultResilientRetiredDelayMilliseconds).Milliseconds(),
            DefaultResilientRetiredCount);
    }

    protected ILogger CreateLogger()
    {
        return rootServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(PlatformOutboxMessageBusProducerHelper));
    }

    protected static void LogSendOutboxMessageFailed(Exception exception,
        PlatformOutboxBusMessage existingOutboxMessage, ILogger logger)
    {
        logger.LogError(exception,
            "Error Send message [Type:{ExistingOutboxMessage_MessageTypeFullName}]; [[Error:{Error}]]. " +
            "Message Info: ${ExistingOutboxMessage_JsonMessage}.",
            existingOutboxMessage.MessageTypeFullName,
            exception.Message,
            existingOutboxMessage.JsonMessage);
    }
}