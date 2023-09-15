using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context;
using UMS.Platform.Common;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Hosting;
using UMS.Platform.Common.JsonSerialization;
using UMS.Platform.Common.Utils;
using UMS.Platform.Domain.Exceptions;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus.OutboxPattern;

/// <summary>
///     Run in an interval to scan messages in OutboxCollection in database, check new message to send it
/// </summary>
public class PlatformSendOutboxBusMessageHostedService : PlatformIntervalProcessHostedService
{
    public const int MinimumRetrySendOutboxMessageTimesToWarning = 3;

    private readonly IPlatformApplicationSettingContext applicationSettingContext;
    private bool isProcessing;

    public PlatformSendOutboxBusMessageHostedService(IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformOutboxConfig outboxConfig) : base(serviceProvider, loggerFactory)
    {
        this.applicationSettingContext = applicationSettingContext;
        OutboxConfig = outboxConfig;
    }

    protected PlatformOutboxConfig OutboxConfig { get; }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        if (!HasOutboxEventBusMessageRepositoryRegistered() || isProcessing) return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the db is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(() => SendOutboxEventBusMessages(cancellationToken),
                retryAttempt => 10.Seconds(),
                ProcessSendMessageRetryCount(),
                onRetry: (ex, timeSpan, currentRetry,
                    ctx) =>
                {
                    if (currentRetry >= MinimumRetrySendOutboxMessageTimesToWarning)
                        Logger.LogWarning(ex,
                            "Retry SendOutboxEventBusMessages {CurrentRetry} time(s) failed. [ApplicationName:{ApplicationName}]. [ApplicationAssembly:{ApplicationAssembly_FullName}]",
                            currentRetry,
                            applicationSettingContext.ApplicationName,
                            applicationSettingContext.ApplicationAssembly.FullName);
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "SendOutboxEventBusMessages failed. [[Error:{Error}]]. [ApplicationName:{ApplicationName}]. [ApplicationAssembly:{ApplicationAssembly_FullName}]",
                ex.Message,
                applicationSettingContext.ApplicationName,
                applicationSettingContext.ApplicationAssembly.FullName);
        }

        isProcessing = false;
    }

    protected virtual async Task SendOutboxEventBusMessages(CancellationToken cancellationToken)
    {
        do
        {
            var toHandleMessages = await PopToHandleOutboxEventBusMessages(cancellationToken);

            await toHandleMessages
                .ParallelAsync(async toHandleOutboxMessage =>
                {
                    using (var scope = ServiceProvider.CreateScope())
                    {
                        try
                        {
                            await SendMessageToBusAsync(scope,
                                toHandleOutboxMessage,
                                OutboxConfig.RetryProcessFailedMessageInSecondsUnit,
                                cancellationToken);
                        }
                        catch (Exception e)
                        {
                            Logger.LogError(e,
                                "[PlatformSendOutboxEventBusMessageHostedService] Failed to produce outbox message. [[Error:{Error}]]" +
                                "Id:{OutboxMessageId} failed. " +
                                "Message Content:{OutboxMessage}",
                                e.Message,
                                toHandleOutboxMessage.Id,
                                toHandleOutboxMessage.ToJson());
                        }
                    }
                });
        } while (await IsAnyMessagesToHandleAsync());
    }

    protected Task<bool> IsAnyMessagesToHandleAsync()
    {
        return ServiceProvider.ExecuteInjectScopedAsync<bool>(
            (IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
            {
                return outboxEventBusMessageRepo!.AnyAsync(
                    PlatformOutboxBusMessage.CanHandleMessagesExpr(MessageProcessingMaximumTimeInSeconds()));
            });
    }

    protected virtual async Task SendMessageToBusAsync(IServiceScope scope,
        PlatformOutboxBusMessage toHandleOutboxMessage,
        double retryProcessFailedMessageInSecondsUnit,
        CancellationToken cancellationToken)
    {
        await scope.ExecuteInjectAsync(async (PlatformOutboxMessageBusProducerHelper outboxEventBusProducerHelper) =>
        {
            var messageType = ResolveMessageType(toHandleOutboxMessage);

            if (messageType != null)
            {
                var message = PlatformJsonSerializer.Deserialize(toHandleOutboxMessage.JsonMessage,
                    messageType);

                await outboxEventBusProducerHelper!.HandleSendingOutboxMessageAsync(message,
                    toHandleOutboxMessage.RoutingKey,
                    retryProcessFailedMessageInSecondsUnit,
                    toHandleOutboxMessage,
                    null,
                    cancellationToken);
            }
            else
            {
                await outboxEventBusProducerHelper.UpdateExistingOutboxMessageFailedInNewScopeAsync(
                    toHandleOutboxMessage,
                    new Exception($"[{GetType().Name}] Error resolve outbox message type " +
                                  $"[TypeName:{toHandleOutboxMessage.MessageTypeFullName}]. OutboxId:{toHandleOutboxMessage.Id}"),
                    retryProcessFailedMessageInSecondsUnit,
                    cancellationToken,
                    Logger);
            }
        });
    }

    protected async Task<List<PlatformOutboxBusMessage>> PopToHandleOutboxEventBusMessages(
        CancellationToken cancellationToken)
    {
        try
        {
            return await ServiceProvider.ExecuteInjectScopedAsync<List<PlatformOutboxBusMessage>>(
                async (IUnitOfWorkManager uowManager, IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
                {
                    using (var uow = uowManager.Begin())
                    {
                        var toHandleMessages = await outboxEventBusMessageRepo.GetAllAsync(query => query
                                .Where(PlatformOutboxBusMessage.CanHandleMessagesExpr(
                                    MessageProcessingMaximumTimeInSeconds()))
                                .OrderBy(p => p.LastSendDate)
                                .Take(NumberOfProcessSendOutboxMessagesBatch()),
                            cancellationToken);

                        toHandleMessages.ForEach(p =>
                        {
                            p.SendStatus = PlatformOutboxBusMessage.SendStatuses.Processing;
                        });

                        await outboxEventBusMessageRepo.UpdateManyAsync(toHandleMessages,
                            cancellationToken: cancellationToken);

                        await uow.CompleteAsync(cancellationToken);

                        return toHandleMessages;
                    }
                });
        }
        catch (PlatformDomainRowVersionConflictException conflictDomainException)
        {
            Logger.LogWarning(conflictDomainException,
                "Some other producer instance has been handling some outbox messages, which lead to row version conflict (support multi service instance running concurrently). This is as expected so just warning.");

            // WHY: Because support multi service instance running concurrently,
            // get row version conflict is expected, so just retry again to get unprocessed outbox messages
            return await PopToHandleOutboxEventBusMessages(cancellationToken);
        }
    }

    protected virtual int NumberOfProcessSendOutboxMessagesBatch()
    {
        return OutboxConfig.NumberOfProcessSendOutboxMessagesBatch;
    }

    protected virtual int ProcessSendMessageRetryCount()
    {
        return OutboxConfig.ProcessSendMessageRetryCount;
    }

    /// <inheritdoc cref="PlatformOutboxConfig.MessageProcessingMaxSeconds" />
    protected virtual double MessageProcessingMaximumTimeInSeconds()
    {
        return OutboxConfig.MessageProcessingMaxSeconds;
    }

    protected bool HasOutboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope =>
            scope.ServiceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null);
    }

    private Type ResolveMessageType(PlatformOutboxBusMessage toHandleOutboxMessage)
    {
        var messageType =
            Type.GetType(toHandleOutboxMessage.MessageTypeFullName, false) ??
            ServiceProvider
                .GetService<IPlatformMessageBusScanner>()!
                .ScanAssemblies()
                .ConcatSingle(typeof(PlatformModule).Assembly)
                .Select(assembly => assembly.GetType(toHandleOutboxMessage.MessageTypeFullName))
                .FirstOrDefault(p => p != null);

        return messageType;
    }
}