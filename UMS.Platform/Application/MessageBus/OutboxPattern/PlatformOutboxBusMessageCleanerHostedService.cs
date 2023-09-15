using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Hosting;
using UMS.Platform.Common.Utils;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Application.MessageBus.OutboxPattern;

public class PlatformOutboxBusMessageCleanerHostedService : PlatformIntervalProcessHostedService
{
    public const int MinimumRetryCleanOutboxMessageTimesToWarning = 3;

    private readonly IPlatformApplicationSettingContext applicationSettingContext;

    protected readonly PlatformOutboxConfig OutboxConfig;

    private bool isProcessing;

    public PlatformOutboxBusMessageCleanerHostedService(IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IPlatformApplicationSettingContext applicationSettingContext,
        PlatformOutboxConfig outboxConfig) : base(serviceProvider, loggerFactory)
    {
        this.applicationSettingContext = applicationSettingContext;
        OutboxConfig = outboxConfig;
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        if (!HasOutboxEventBusMessageRepositoryRegistered() || isProcessing) return;

        isProcessing = true;

        try
        {
            // WHY: Retry in case of the db is not started, initiated or restarting
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(() => CleanOutboxEventBusMessage(cancellationToken),
                retryAttempt => 10.Seconds(),
                ProcessClearMessageRetryCount(),
                onRetry: (ex, timeSpan, currentRetry,
                    ctx) =>
                {
                    if (currentRetry >= MinimumRetryCleanOutboxMessageTimesToWarning)
                        Logger.LogWarning(ex,
                            "Retry CleanOutboxEventBusMessage {CurrentRetry} time(s) failed. [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                            currentRetry,
                            applicationSettingContext.ApplicationName,
                            applicationSettingContext.ApplicationAssembly.FullName);
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "CleanOutboxEventBusMessage failed. [[Error:{Error}]] [ApplicationName:{ApplicationSettingContext.ApplicationName}]. [ApplicationAssembly:{ApplicationSettingContext.ApplicationAssembly.FullName}]",
                ex.Message,
                applicationSettingContext.ApplicationName,
                applicationSettingContext.ApplicationAssembly.FullName);
        }

        isProcessing = false;
    }

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return OutboxConfig.MessageCleanerTriggerIntervalInMinutes.Minutes();
    }

    protected virtual int ProcessClearMessageRetryCount()
    {
        return OutboxConfig.ProcessClearMessageRetryCount;
    }

    /// <inheritdoc cref="PlatformOutboxConfig.NumberOfDeleteMessagesBatch" />
    protected virtual int NumberOfDeleteMessagesBatch()
    {
        return OutboxConfig.NumberOfDeleteMessagesBatch;
    }

    /// <inheritdoc cref="PlatformOutboxConfig.DeleteProcessedMessageInSeconds" />
    protected virtual double DeleteProcessedMessageInSeconds()
    {
        return OutboxConfig.DeleteProcessedMessageInSeconds;
    }

    /// <inheritdoc cref="PlatformOutboxConfig.DeleteExpiredFailedMessageInSeconds" />
    protected virtual double DeleteExpiredFailedMessageInSeconds()
    {
        return OutboxConfig.DeleteExpiredFailedMessageInSeconds;
    }

    protected bool HasOutboxEventBusMessageRepositoryRegistered()
    {
        return ServiceProvider.ExecuteScoped(scope =>
            scope.ServiceProvider.GetService<IPlatformOutboxBusMessageRepository>() != null);
    }

    protected async Task CleanOutboxEventBusMessage(CancellationToken cancellationToken)
    {
        var totalProcessedMessages = await ServiceProvider.ExecuteScopedAsync(p =>
            p.ServiceProvider.GetRequiredService<IPlatformOutboxBusMessageRepository>()
                .CountAsync(p => p.SendStatus == PlatformOutboxBusMessage.SendStatuses.Processed, cancellationToken));

        if (totalProcessedMessages > OutboxConfig.MaxStoreProcessedMessageCount)
            await ProcessCleanMessageByMaxStoreProcessedMessageCount(totalProcessedMessages, cancellationToken);
        else
            await ProcessCleanMessageByExpiredTime(cancellationToken);
    }

    private async Task ProcessCleanMessageByMaxStoreProcessedMessageCount(int totalProcessedMessages,
        CancellationToken cancellationToken)
    {
        await ServiceProvider.ExecuteInjectScopedScrollingPagingAsync<PlatformOutboxBusMessage>(
            await ServiceProvider.ExecuteScopedAsync(p =>
                p.ServiceProvider.GetRequiredService<IPlatformOutboxBusMessageRepository>()
                    .CountAsync(CleanMessagePredicate(), cancellationToken)
                    .Then(total => total / NumberOfDeleteMessagesBatch())),
            async (IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
            {
                var toDeleteMessages = await outboxEventBusMessageRepo.GetAllAsync(query => query
                        .Where(CleanMessagePredicate())
                        .OrderByDescending(p => p.LastSendDate)
                        .Skip(OutboxConfig.MaxStoreProcessedMessageCount)
                        .Take(NumberOfDeleteMessagesBatch()),
                    cancellationToken);

                if (toDeleteMessages.Count > 0)
                    await outboxEventBusMessageRepo.DeleteManyAsync(toDeleteMessages,
                        true,
                        null,
                        cancellationToken);

                return toDeleteMessages;
            });

        Logger.LogInformation("CleanOutboxEventBusMessage success. Number of deleted messages: {DeletedMessageCount}",
            totalProcessedMessages - OutboxConfig.MaxStoreProcessedMessageCount);

        static Expression<Func<PlatformOutboxBusMessage, bool>> CleanMessagePredicate()
        {
            return p => p.SendStatus == PlatformOutboxBusMessage.SendStatuses.Processed;
        }
    }

    private async Task ProcessCleanMessageByExpiredTime(CancellationToken cancellationToken)
    {
        var toCleanMessageCount = await ServiceProvider.ExecuteScoped(scope =>
            scope.ServiceProvider.GetRequiredService<IPlatformOutboxBusMessageRepository>()
                .CountAsync(
                    PlatformOutboxBusMessage.ToCleanExpiredMessagesByTimeExpr(DeleteProcessedMessageInSeconds(),
                        DeleteExpiredFailedMessageInSeconds()),
                    cancellationToken));

        if (toCleanMessageCount > 0)
        {
            await ServiceProvider.ExecuteInjectScopedScrollingPagingAsync<PlatformOutboxBusMessage>(
                toCleanMessageCount / NumberOfDeleteMessagesBatch(),
                async (IUnitOfWorkManager uowManager, IPlatformOutboxBusMessageRepository outboxEventBusMessageRepo) =>
                {
                    using (var uow = uowManager!.Begin())
                    {
                        var expiredMessages = await outboxEventBusMessageRepo.GetAllAsync(query => query
                                .Where(PlatformOutboxBusMessage.ToCleanExpiredMessagesByTimeExpr(
                                    DeleteProcessedMessageInSeconds(),
                                    DeleteExpiredFailedMessageInSeconds()))
                                .OrderBy(p => p.LastSendDate)
                                .Take(NumberOfDeleteMessagesBatch()),
                            cancellationToken);

                        if (expiredMessages.Count > 0)
                        {
                            await outboxEventBusMessageRepo.DeleteManyAsync(expiredMessages,
                                true,
                                null,
                                cancellationToken);

                            await uow.CompleteAsync(cancellationToken);
                        }

                        return expiredMessages;
                    }
                });

            Logger.LogInformation(
                "CleanOutboxEventBusMessage success. Number of deleted messages: {ToCleanMessageCount}",
                toCleanMessageCount);
        }
    }
}