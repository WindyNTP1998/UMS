using UMS.Platform.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMS.Platform.Common;
using UMS.Platform.Common.DependencyInjection;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Utils;

namespace UMS.Platform.Infrastructures.BackgroundJob;

public abstract class PlatformBackgroundJobModule : PlatformInfrastructureModule
{
    // PlatformBackgroundJobModule init after PersistenceModule but before other modules
    public new const int DefaultExecuteInitPriority = DefaultDependentOnPersistenceInitExecuteInitPriority;

    public PlatformBackgroundJobModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    public override int ExecuteInitPriority => DefaultExecuteInitPriority;

    public static int DefaultStartBackgroundJobProcessingRetryCount => PlatformEnvironment.IsDevelopment ? 5 : 10;

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobExecutor>(Assembly);

        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobScheduler>(Assembly,
            replaceStrategy: DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobProcessingService>(Assembly,
            ServiceLifeTime.Singleton,
            replaceStrategy: DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

        serviceCollection.RegisterHostedService<PlatformBackgroundJobStartProcessHostedService>();
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        await base.InternalInit(serviceScope);

        await ReplaceAllLatestRecurringBackgroundJobs(serviceScope);

        await StartBackgroundJobProcessing(serviceScope);
    }

    public async Task StartBackgroundJobProcessing(IServiceScope serviceScope)
    {
        var backgroundJobProcessingService =
            serviceScope.ServiceProvider.GetRequiredService<IPlatformBackgroundJobProcessingService>();

        if (!backgroundJobProcessingService.Started())
            await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(async () =>
                {
                    await backgroundJobProcessingService.Start();

                    Util.TaskRunner.QueueActionInBackground(ExecuteOnStartUpRecurringBackgroundJobImmediately,
                        () => Logger);
                },
                retryAttempt => 10.Seconds(),
                DefaultStartBackgroundJobProcessingRetryCount,
                onRetry: (exception, timeSpan, currentRetry,
                    ctx) =>
                {
                    if (currentRetry >= MinimumRetryTimesToWarning)
                        LoggerFactory.CreateLogger(typeof(PlatformBackgroundJobModule))
                            .LogWarning(exception,
                                "[StartBackgroundJobProcessing] Exception {ExceptionType} detected on attempt StartBackgroundJobProcessing {Retry} of {Retries}",
                                exception.GetType().Name,
                                currentRetry,
                                DefaultStartBackgroundJobProcessingRetryCount);
                });
    }

    public async Task ExecuteOnStartUpRecurringBackgroundJobImmediately()
    {
        await Task.Run(() => IPlatformModule.WaitAllModulesInitiated(ServiceProvider, typeof(IPlatformModule), Logger,
            "execute on start-up recurring background job"));

        await ServiceProvider.ExecuteInjectScopedAsync(
            (IPlatformBackgroundJobScheduler backgroundJobScheduler, IServiceProvider serviceProvider) =>
            {
                var allExecuteOnStartUpCurrentRecurringJobExecutors = serviceProvider
                    .GetServices<IPlatformBackgroundJobExecutor>()
                    .Where(p => PlatformRecurringJobAttribute.GetRecurringJobAttributeInfo(p.GetType()) is
                        { ExecuteOnStartUp: true })
                    .ToList();

                allExecuteOnStartUpCurrentRecurringJobExecutors.ForEach(p =>
                    backgroundJobScheduler.Schedule<object>(p.GetType(), null, DateTimeOffset.UtcNow));
            });
    }

    public async Task ReplaceAllLatestRecurringBackgroundJobs(IServiceScope serviceScope)
    {
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(async () =>
            {
                var scheduler = serviceScope.ServiceProvider.GetRequiredService<IPlatformBackgroundJobScheduler>();

                var allCurrentRecurringJobExecutors = serviceScope.ServiceProvider
                    .GetServices<IPlatformBackgroundJobExecutor>()
                    .Where(p => PlatformRecurringJobAttribute.GetRecurringJobAttributeInfo(p.GetType()) != null)
                    .ToList();

                scheduler.ReplaceAllRecurringBackgroundJobs(allCurrentRecurringJobExecutors);
            },
            retryAttempt => 10.Seconds(),
            DefaultStartBackgroundJobProcessingRetryCount,
            onRetry: (exception, timeSpan, currentRetry,
                ctx) =>
            {
                if (currentRetry >= MinimumRetryTimesToWarning)
                    LoggerFactory.CreateLogger(typeof(PlatformBackgroundJobModule))
                        .LogWarning(exception,
                            "[Init][ReplaceAllLatestRecurringBackgroundJobs] Exception {ExceptionType} detected on attempt ReplaceAllLatestRecurringBackgroundJobs {Retry} of {Retries}",
                            exception.GetType().Name,
                            currentRetry,
                            DefaultStartBackgroundJobProcessingRetryCount);
            });
    }
}