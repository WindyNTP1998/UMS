using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMS.Platform.Application.BackgroundJob;
using UMS.Platform.Application.Context;
using UMS.Platform.Application.Context.UserContext;
using UMS.Platform.Application.Context.UserContext.Default;
using UMS.Platform.Application.Cqrs.Commands;
using UMS.Platform.Application.Cqrs.Events;
using UMS.Platform.Application.Cqrs.Queries;
using UMS.Platform.Application.Domain;
using UMS.Platform.Application.HostingBackgroundServices;
using UMS.Platform.Application.MessageBus;
using UMS.Platform.Application.MessageBus.Consumers;
using UMS.Platform.Application.MessageBus.InboxPattern;
using UMS.Platform.Application.MessageBus.OutboxPattern;
using UMS.Platform.Application.MessageBus.Producers;
using UMS.Platform.Application.MessageBus.Producers.CqrsEventProducers;
using UMS.Platform.Application.Persistence;
using UMS.Platform.Application.Services;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs.Events;
using UMS.Platform.Common.DependencyInjection;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Utils;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.Abstract;
using UMS.Platform.Infrastructures.BackgroundJob;
using UMS.Platform.Infrastructures.Caching;
using UMS.Platform.Infrastructures.MessageBus;
using UMS.Platform.Persistence;

namespace UMS.Platform.Application;

public interface IPlatformApplicationModule : IPlatformModule
{
    Task SeedData(IServiceScope serviceScope);

    Task ClearDistributedCache(PlatformApplicationAutoClearDistributedCacheOnInitOptions options,
        IServiceScope serviceScope);
}

public abstract class PlatformApplicationModule : PlatformModule, IPlatformApplicationModule
{
    protected PlatformApplicationModule(IServiceProvider serviceProvider,
        IConfiguration configuration) : base(serviceProvider, configuration)
    {
    }

    protected override bool AutoScanAssemblyRegisterCqrs => true;

    /// <summary>
    ///     Override this to true to auto register default caching module, which include default memory caching repository.
    ///     <br></br>
    ///     Don't need to auto register if you have register a caching module manually
    /// </summary>
    protected virtual bool AutoRegisterDefaultCaching => true;

    /// <summary>
    ///     Default is True. Override this return to False if you need to seed data manually
    /// </summary>
    protected virtual bool AutoSeedApplicationDataOnInit => true;

    /// <summary>
    ///     Set min thread pool then default to increase and fix some performance issues. Article:
    ///     https://medium.com/@jaiadityarathore/dotnet-core-threadpool-bef2f5a37888
    ///     https://github.com/StackExchange/StackExchange.Redis/issues/2332
    /// </summary>
    protected virtual int MinThreadPool => 100;

    public async Task SeedData(IServiceScope serviceScope)
    {
        //if the db server is not initiated, SeedData could fail.
        //So that we do retry to ensure that SeedData action run successfully.
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(async () =>
            {
                var dataSeeders = serviceScope.ServiceProvider
                    .GetServices<IPlatformApplicationDataSeeder>()
                    .DistinctBy(p => p.GetType())
                    .OrderBy(p => p.DelaySeedingInBackgroundBySeconds)
                    .ThenBy(p => p.SeedOrder);

                await dataSeeders.ForEachAsync(async seeder =>
                {
                    if (seeder.DelaySeedingInBackgroundBySeconds > 0)
                    {
                        Logger.LogInformation(
                            "[SeedData] {Seeder} is scheduled running in background after {DelaySeedingInBackgroundBySeconds} seconds.",
                            seeder.GetType().Name,
                            seeder.DelaySeedingInBackgroundBySeconds);

                        Util.TaskRunner.QueueActionInBackground(
                            async () => await ExecuteSeedingWithNewScopeInBackground(seeder.GetType(), Logger),
                            () => CreateLogger(LoggerFactory),
                            seeder.DelaySeedingInBackgroundBySeconds);
                    }
                    else
                    {
                        await ExecuteDataSeederWithLog(seeder, Logger);
                    }
                });
            },
            retryAttempt => 10.Seconds(),
            10,
            onRetry: (exception, timeSpan, retry,
                ctx) =>
            {
                if (retry >= MinimumRetryTimesToWarning)
                    Logger.LogWarning(exception,
                        "Exception {ExceptionType} detected on attempt SeedData {Retry}",
                        exception.GetType().Name,
                        retry);
            });

        // Need to execute in background with service instance new scope
        // if not, the scope will be disposed, which lead to the seed data will be failed
        async Task ExecuteSeedingWithNewScopeInBackground(Type seederType, ILogger logger)
        {
            try
            {
                await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(async () =>
                    {
                        using (var newScope = ServiceProvider.CreateScope())
                        {
                            var dataSeeder = newScope.ServiceProvider
                                .GetServices<IPlatformApplicationDataSeeder>()
                                .First(_ => _.GetType() == seederType);

                            await ExecuteDataSeederWithLog(dataSeeder, logger);
                        }
                    },
                    retryAttempt => 15.Seconds(),
                    20,
                    onRetry: (ex, timeSpan, currentRetry,
                        context) =>
                    {
                        if (currentRetry >= MinimumRetryTimesToWarning)
                            logger.LogWarning(ex,
                                "[SeedData] Retry seed data in background {SeederTypeName}.",
                                seederType.Name);
                    });
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[SeedData] Seed data in background {SeederTypeName} failed.",
                    seederType.Name);
            }
        }

        static async Task ExecuteDataSeederWithLog(IPlatformApplicationDataSeeder dataSeeder, ILogger logger)
        {
            logger.LogInformation("[SeedData] {DataSeeder} STARTED.", dataSeeder.GetType().Name);

            await dataSeeder.SeedData();

            logger.LogInformation("[SeedData] {DataSeeder} FINISHED.", dataSeeder.GetType().Name);
        }
    }

    public async Task ClearDistributedCache(PlatformApplicationAutoClearDistributedCacheOnInitOptions options,
        IServiceScope serviceScope)
    {
        //if the cache server is not initiated, ClearDistributedCache could fail.
        //So that we do retry to ensure that ClearDistributedCache action run successfully.
        await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(async () =>
            {
                var cacheProvider = serviceScope.ServiceProvider.GetService<IPlatformCacheRepositoryProvider>();

                var distributedCacheRepository = cacheProvider?.TryGet(PlatformCacheRepositoryType.Distributed);

                if (distributedCacheRepository != null)
                    await distributedCacheRepository.RemoveAsync(p => options.AutoClearContexts.Contains(p.Context));
            },
            retryAttempt => 10.Seconds(),
            10,
            onRetry: (exception, timeSpan, retry,
                ctx) =>
            {
                if (retry >= MinimumRetryTimesToWarning)
                    Logger.LogWarning(exception,
                        "Exception {ExceptionType} detected on attempt ClearDistributedCache {Retry}",
                        exception.GetType().Name,
                        retry);
            });
    }

    public override string[] TracingSources()
    {
        return Util.ListBuilder.NewArray(IPlatformCqrsCommandApplicationHandler.ActivitySource.Name,
            IPlatformCqrsQueryApplicationHandler.ActivitySource.Name,
            IPlatformApplicationBackgroundJobExecutor.ActivitySource.Name,
            IPlatformCqrsEventHandler.ActivitySource.Name);
    }

    /// <summary>
    ///     Support to custom the inbox config. Default return null
    /// </summary>
    protected virtual PlatformInboxConfig InboxConfigProvider(IServiceProvider serviceProvider)
    {
        return new PlatformInboxConfig();
    }

    /// <summary>
    ///     Support to custom the outbox config. Default return null
    /// </summary>
    protected virtual PlatformOutboxConfig OutboxConfigProvider(IServiceProvider serviceProvider)
    {
        return new PlatformOutboxConfig();
    }

    protected override void RegisterHelpers(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformHelper>(typeof(PlatformApplicationModule).Assembly);
        serviceCollection.RegisterAllFromType<IPlatformHelper>(Assembly);
    }

    public static async Task ExecuteDependencyApplicationModuleSeedData(List<Type> moduleTypeDependencies,
        IServiceProvider serviceProvider)
    {
        await moduleTypeDependencies
            .Where(moduleType => moduleType.IsAssignableTo(typeof(IPlatformApplicationModule)))
            .Select(moduleType => new
            {
                ModuleType = moduleType,
                serviceProvider.GetService(moduleType).As<IPlatformApplicationModule>().ExecuteInitPriority
            })
            .OrderByDescending(p => p.ExecuteInitPriority)
            .Select(p => p.ModuleType)
            .ForEachAsync(async moduleType =>
            {
                await serviceProvider.ExecuteScopedAsync(scope =>
                    scope.ServiceProvider.GetService(moduleType).As<IPlatformApplicationModule>().SeedData(scope));
            });
    }

    public async Task ExecuteDependencyApplicationModuleSeedData()
    {
        await ExecuteDependencyApplicationModuleSeedData(
            ModuleTypeDependencies().Select(moduleTypeProvider => moduleTypeProvider(Configuration)).ToList(),
            ServiceProvider);
    }

    /// <summary>
    ///     Override this factory method to register default PlatformApplicationSettingContext if application do not
    ///     have any implementation of IPlatformApplicationSettingContext in the Assembly to be registered.
    /// </summary>
    protected virtual PlatformApplicationSettingContext DefaultApplicationSettingContextFactory(
        IServiceProvider serviceProvider)
    {
        return new PlatformApplicationSettingContext
        {
            ApplicationName = Assembly.GetName().Name,
            ApplicationAssembly = Assembly
        };
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformApplicationDataSeeder>(Assembly, ServiceLifeTime.Scoped);
        serviceCollection.RegisterAllSelfImplementationFromType<IPlatformCqrsEventApplicationHandler>(Assembly);
        RegisterMessageBus(serviceCollection);
        RegisterApplicationSettingContext(serviceCollection);
        RegisterDefaultApplicationUserContext(serviceCollection);
        serviceCollection.RegisterIfServiceNotExist<IUnitOfWorkManager, PlatformPseudoApplicationUnitOfWorkManager>(
            ServiceLifeTime.Scoped);
        serviceCollection.RegisterAllFromType<IPlatformApplicationService>(Assembly);

        serviceCollection.RegisterAllFromType<IPlatformDbContext>(Assembly, ServiceLifeTime.Scoped);
        serviceCollection.RegisterAllFromType<IPlatformInfrastructureService>(Assembly);
        serviceCollection.RegisterAllFromType<IPlatformBackgroundJobExecutor>(Assembly);

        if (AutoRegisterDefaultCaching) RegisterRuntimeModuleDependencies<PlatformCachingModule>(serviceCollection);

        serviceCollection.RegisterHostedService<PlatformAutoClearMemoryHostingBackgroundService>();
    }

    protected override async Task InternalInit(IServiceScope serviceScope)
    {
        ThreadPool.SetMinThreads(MinThreadPool, MinThreadPool);

        await IPlatformPersistenceModule.ExecuteDependencyPersistenceModuleMigrateApplicationData(
            ModuleTypeDependencies().Select(moduleTypeProvider => moduleTypeProvider(Configuration)).ToList(),
            ServiceProvider);

        if (IsRootModule && AutoSeedApplicationDataOnInit) await ExecuteDependencyApplicationModuleSeedData();

        var autoClearDistributedCacheOnInitOptions = AutoClearDistributedCacheOnInitOptions(serviceScope);
        if (autoClearDistributedCacheOnInitOptions.EnableAutoClearDistributedCacheOnInit)
            await ClearDistributedCache(autoClearDistributedCacheOnInitOptions, serviceScope);

        if (AutoRegisterDefaultCaching)
            await serviceScope.ServiceProvider.GetRequiredService<PlatformCachingModule>().Init();
    }

    protected virtual PlatformApplicationAutoClearDistributedCacheOnInitOptions
        AutoClearDistributedCacheOnInitOptions(IServiceScope serviceScope)
    {
        var applicationSettingContext =
            serviceScope.ServiceProvider.GetRequiredService<IPlatformApplicationSettingContext>();

        return new PlatformApplicationAutoClearDistributedCacheOnInitOptions
        {
            EnableAutoClearDistributedCacheOnInit = true,
            AutoClearContexts = new HashSet<string>
            {
                applicationSettingContext.ApplicationName
            }
        };
    }

    private void RegisterApplicationSettingContext(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformApplicationSettingContext>(Assembly);

        // If there is no custom implemented class type of IPlatformApplicationSettingContext in application,
        // register default PlatformApplicationSettingContext from result of DefaultApplicationSettingContextFactory
        // WHY: To support custom IPlatformApplicationSettingContext if you want to or just use the default from DefaultApplicationSettingContextFactory
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformApplicationSettingContext)))
            serviceCollection.Register<IPlatformApplicationSettingContext>(DefaultApplicationSettingContextFactory);
    }

    private static void RegisterDefaultApplicationUserContext(IServiceCollection serviceCollection)
    {
        if (serviceCollection.All(p => p.ServiceType != typeof(IPlatformApplicationUserContextAccessor)))
            serviceCollection.Register(typeof(IPlatformApplicationUserContextAccessor),
                typeof(PlatformDefaultApplicationUserContextAccessor),
                ServiceLifeTime.Singleton,
                true,
                DependencyInjectionExtension.CheckRegisteredStrategy.ByService);
    }

    private void RegisterMessageBus(IServiceCollection serviceCollection)
    {
        serviceCollection.Register<IPlatformMessageBusScanner, PlatformApplicationMessageBusScanner>(ServiceLifeTime
            .Singleton);

        serviceCollection.Register<IPlatformApplicationBusMessageProducer, PlatformApplicationBusMessageProducer>();
        serviceCollection.RegisterAllSelfImplementationFromType(typeof(IPlatformCqrsEventBusMessageProducer<>),
            Assembly);
        serviceCollection.RegisterAllSelfImplementationFromType(typeof(PlatformCqrsCommandEventBusMessageProducer<>),
            Assembly);
        serviceCollection.RegisterAllSelfImplementationFromType(typeof(PlatformCqrsEntityEventBusMessageProducer<,>),
            Assembly);

        serviceCollection.RegisterAllSelfImplementationFromType(typeof(IPlatformMessageBusConsumer),
            typeof(PlatformApplicationModule).Assembly);
        serviceCollection.RegisterAllSelfImplementationFromType(typeof(IPlatformMessageBusConsumer),
            Assembly);
        serviceCollection.RegisterAllSelfImplementationFromType(typeof(IPlatformApplicationMessageBusConsumer<>),
            Assembly);

        serviceCollection.RegisterHostedService<PlatformInboxBusMessageCleanerHostedService>();
        serviceCollection.RegisterHostedService<PlatformConsumeInboxBusMessageHostedService>();
        serviceCollection.Register(typeof(PlatformInboxConfig),
            InboxConfigProvider,
            ServiceLifeTime.Transient,
            true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

        serviceCollection.RegisterHostedService<PlatformOutboxBusMessageCleanerHostedService>();
        serviceCollection.RegisterHostedService<PlatformSendOutboxBusMessageHostedService>();
        serviceCollection.Register(typeof(PlatformOutboxConfig),
            OutboxConfigProvider,
            ServiceLifeTime.Transient,
            true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);
    }
}

public class PlatformApplicationAutoClearDistributedCacheOnInitOptions
{
    private HashSet<string> autoClearContexts;
    public bool EnableAutoClearDistributedCacheOnInit { get; set; }

    public HashSet<string> AutoClearContexts
    {
        get => autoClearContexts;
        set => autoClearContexts = value?.Select(PlatformCacheKey.AutoFixKeyPartValue).ToHashSet();
    }
}