using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using UMS.Platform.Common.Cqrs;
using UMS.Platform.Common.DependencyInjection;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.JsonSerialization;
using UMS.Platform.Common.Utils;

namespace UMS.Platform.Common;

public interface IPlatformModule
{
    public const int DefaultMaxWaitModuleInitiatedSeconds = 86400 * 5;

    public const string DefaultLogCategory = "Easy.Platform";

    /// <summary>
    ///     Higher Priority value mean the module init will be executed before lower Priority value in the same level module
    ///     dependencies
    ///     <br />
    ///     Default is 10. For the default priority should be:  InfrastructureModule (Not Dependent on DatabaseInitialization)
    ///     => PersistenceModule => InfrastructureModule (Dependent on DatabaseInitialization) => Others Module (10)
    /// </summary>
    public int ExecuteInitPriority { get; }

    public IServiceCollection ServiceCollection { get; }
    public IServiceProvider ServiceProvider { get; }
    public IConfiguration Configuration { get; }
    public bool IsChildModule { get; set; }
    public bool IsRootModule => CheckIsRootModule(this);

    /// <summary>
    ///     Current runtime module instance Assembly
    /// </summary>
    public Assembly Assembly { get; }

    public bool RegisterServicesExecuted { get; }
    public bool Initiated { get; }

    public Action<TracerProviderBuilder> AdditionalTracingConfigure { get; }

    public static ILogger CreateDefaultLogger(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(DefaultLogCategory);
    }

    public static void WaitAllModulesInitiated(IServiceProvider serviceProvider, Type moduleType, ILogger logger = null,
        string logSuffix = null)
    {
        if (serviceProvider.GetServices(moduleType).Select(p => p.As<IPlatformModule>()).All(p => p.Initiated)) return;

        var useLogger = logger ?? CreateDefaultLogger(serviceProvider);

        useLogger.LogInformation("[Platform] Start WaitAllModulesInitiated of type {ModuleType} {LogSuffix} STARTED",
            moduleType.Name, logSuffix);

        Util.TaskRunner.WaitUntil(() =>
            {
                var modules = serviceProvider.GetServices(moduleType).Select(p => p.As<IPlatformModule>());

                return modules.All(p => p.Initiated);
            },
            serviceProvider.GetServices(moduleType).Count() * DefaultMaxWaitModuleInitiatedSeconds,
            waitForMsg: $"Wait for all modules of type {moduleType.Name} get initiated",
            waitIntervalSeconds: 5);

        useLogger.LogInformation("[Platform] WaitAllModulesInitiated of type {ModuleType} {LogSuffix} FINISHED",
            moduleType.Name, logSuffix);
    }

    public List<IPlatformModule> AllDependencyChildModules(IServiceCollection useServiceCollection = null,
        bool includeDeepChildModules = true);

    public static bool CheckIsRootModule(IPlatformModule module)
    {
        return !module.IsChildModule;
    }

    public void RegisterServices(IServiceCollection serviceCollection);

    public Task Init();

    public List<Func<IConfiguration, Type>> ModuleTypeDependencies();

    /// <summary>
    ///     Override this to call every time a new platform module is registered
    /// </summary>
    public void OnNewOtherModuleRegistered(IServiceCollection serviceCollection,
        PlatformModule newOtherRegisterModule);

    public void RegisterRuntimeModuleDependencies<TModule>(IServiceCollection serviceCollection)
        where TModule : PlatformModule;

    public string[] TracingSources();
}

/// <summary>
///     Example:
///     <br />
///     services.RegisterModule{XXXApiModule}(); Register module into service collection
///     <br />
///     get module service in collection and call module.Init();
///     Init module to start running init for all other modules and this module itself
/// </summary>
public abstract class PlatformModule : IPlatformModule, IDisposable
{
    public const int DefaultExecuteInitPriority = 10;
    public const int ExecuteInitPriorityNextLevelDistance = 10;
    public const int MinimumRetryTimesToWarning = 2;

    protected static readonly ConcurrentDictionary<string, Assembly> ExecutedRegisterByAssemblies = new();

    protected readonly SemaphoreSlim InitLockAsync = new(1, 1);
    protected readonly SemaphoreSlim RegisterLockAsync = new(1, 1);

    public PlatformModule(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        ServiceProvider = serviceProvider;
        Configuration = configuration;
        LoggerFactory = serviceProvider?.GetService<ILoggerFactory>();
        Logger = serviceProvider?.GetService<ILoggerFactory>()?.Pipe(CreateLogger);
    }

    protected ILogger Logger { get; init; }

    protected ILoggerFactory LoggerFactory { get; init; }

    protected virtual bool AutoScanAssemblyRegisterCqrs => false;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public bool IsRootModule => IPlatformModule.CheckIsRootModule(this);

    /// <summary>
    ///     Higher Priority value mean the module init will be executed before lower Priority value in the same level module
    ///     dependencies
    ///     <br />
    ///     Default is 10. For the default priority should be: PersistenceModule => InfrastructureModule => Others Module
    /// </summary>
    public virtual int ExecuteInitPriority => DefaultExecuteInitPriority;

    public IServiceCollection ServiceCollection { get; private set; }
    public IServiceProvider ServiceProvider { get; }
    public IConfiguration Configuration { get; }

    /// <summary>
    ///     True if the module is in a dependency list of other module, not a root module
    /// </summary>
    public bool IsChildModule { get; set; }

    /// <summary>
    ///     Current runtime module instance Assembly
    /// </summary>
    public Assembly Assembly => GetType().Assembly;

    public bool RegisterServicesExecuted { get; protected set; }

    public bool Initiated { get; protected set; }

    /// <summary>
    ///     Override this to call every time a new other module is registered
    /// </summary>
    public virtual void OnNewOtherModuleRegistered(IServiceCollection serviceCollection,
        PlatformModule newOtherRegisterModule)
    {
    }

    public void RegisterRuntimeModuleDependencies<TModule>(IServiceCollection serviceCollection)
        where TModule : PlatformModule
    {
        serviceCollection.RegisterModule<TModule>(true);
    }

    public void RegisterServices(IServiceCollection serviceCollection)
    {
        try
        {
            RegisterLockAsync.Wait();

            if (RegisterServicesExecuted) return;

            ServiceCollection = serviceCollection;
            RegisterAllModuleDependencies(serviceCollection);
            RegisterDefaultLogs(serviceCollection);
            RegisterCqrs(serviceCollection);
            RegisterHelpers(serviceCollection);
            RegisterDistributedTracing(serviceCollection);
            InternalRegister(serviceCollection);
            serviceCollection.Register<IPlatformRootServiceProvider, PlatformRootServiceProvider>(ServiceLifeTime
                .Singleton);

            RegisterServicesExecuted = true;

            if (JsonSerializerCurrentOptions() != null)
                PlatformJsonSerializer.SetCurrentOptions(JsonSerializerCurrentOptions());
        }
        finally
        {
            RegisterLockAsync.Release();
        }
    }

    public virtual async Task Init()
    {
        try
        {
            await InitLockAsync.WaitAsync();

            if (Initiated) return;

            Logger.LogInformation("[PlatformModule] {Module} Init STARTED", GetType().Name);

            await InitAllModuleDependencies();

            using (var scope = ServiceProvider.CreateScope())
            {
                await InternalInit(scope);
            }

            Initiated = true;

            Logger.LogInformation("[PlatformModule] {Module} Init FINISHED", GetType().Name);
        }
        finally
        {
            InitLockAsync.Release();
        }
    }

    /// <summary>
    ///     Get all dependency modules, also init the value of <see cref="IsChildModule" />, which also affect
    ///     <see cref="IsRootModule" />
    /// </summary>
    public List<IPlatformModule> AllDependencyChildModules(IServiceCollection useServiceCollection = null,
        bool includeDeepChildModules = true)
    {
        return ModuleTypeDependencies()
            .Select(moduleTypeProvider =>
            {
                var moduleType = moduleTypeProvider(Configuration);
                var serviceProvider = useServiceCollection?.BuildServiceProvider() ?? ServiceProvider;

                var dependModule = serviceProvider.GetService(moduleType)
                    .As<IPlatformModule>()
                    .Ensure(dependModule => dependModule != null,
                        $"Module {GetType().Name} depend on {moduleType.Name} but Module {moduleType.Name} does not implement IPlatformModule");

                dependModule.IsChildModule = true;

                return includeDeepChildModules
                    ? dependModule.AllDependencyChildModules(useServiceCollection).ConcatSingle(dependModule)
                    : Util.ListBuilder.New(dependModule);
            })
            .Flatten()
            .ToList();
    }

    public virtual string[] TracingSources()
    {
        return Array.Empty<string>();
    }

    public virtual Action<TracerProviderBuilder> AdditionalTracingConfigure => null;

    /// <summary>
    ///     Define list of any modules that this module depend on. The type must be assigned to <see cref="PlatformModule" />.
    ///     Example from a XXXServiceAspNetCoreModule could depend on XXXPlatformApplicationModule and
    ///     XXXPlatformPersistenceModule.
    ///     Example code : return new { config => typeof(XXXPlatformApplicationModule), config =>
    ///     typeof(XXXPlatformPersistenceModule) };
    /// </summary>
    public virtual List<Func<IConfiguration, Type>> ModuleTypeDependencies()
    {
        return new List<Func<IConfiguration, Type>>();
    }

    protected virtual void Dispose(bool isDisposing)
    {
        if (!isDisposing) return;

        InitLockAsync?.Dispose();
        RegisterLockAsync?.Dispose();
    }

    protected void RegisterDistributedTracing(IServiceCollection serviceCollection)
    {
        if (IsRootModule)
        {
            var distributedTracingConfig = ConfigDistributedTracing();
            if (distributedTracingConfig.Enabled)
            {
                // Register only if enabled for other place to check is any distributed tracing config is enabled or not
                serviceCollection.Register(typeof(DistributedTracingConfig),
                    sp => distributedTracingConfig,
                    ServiceLifeTime.Singleton,
                    true,
                    DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

                // Setup OpenTelemetry
                var allDependencyModules = AllDependencyChildModules(serviceCollection);

                var allDependencyModulesTracingSources = allDependencyModules.SelectMany(p => p.TracingSources());

                serviceCollection.AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .SetResourceBuilder(ResourceBuilder.CreateDefault()
                            .AddService(distributedTracingConfig.AppName ?? GetType().Assembly.GetName().Name!))
                        .AddSource(TracingSources().Concat(allDependencyModulesTracingSources).ToArray())
                        .WithIf(AdditionalTracingConfigure != null, AdditionalTracingConfigure)
                        .WithIf(distributedTracingConfig.AdditionalTraceConfig != null,
                            distributedTracingConfig.AdditionalTraceConfig)
                        .WithIf(distributedTracingConfig.AddOtlpExporterConfig != null,
                            _ => _.AddOtlpExporter(distributedTracingConfig.AddOtlpExporterConfig))
                        .WithIf(allDependencyModules.Any(),
                            _ => allDependencyModules
                                .Where(dependencyModule => dependencyModule.AdditionalTracingConfigure != null)
                                .Select(dependencyModule => dependencyModule.AdditionalTracingConfigure)
                                .ForEach(dependencyModuleAdditionalTracingConfigure =>
                                    dependencyModuleAdditionalTracingConfigure(_))));
            }
        }
    }

    public static ILogger CreateLogger(ILoggerFactory loggerFactory)
    {
        return loggerFactory.CreateLogger(typeof(PlatformModule));
    }

    protected static void ExecuteRegisterByAssemblyOnlyOnce(Action<Assembly> action, Assembly assembly,
        string actionName)
    {
        var executedRegisterByAssemblyKey =
            $"Action:{ExecutedRegisterByAssemblies.ContainsKey(actionName)};Assembly:{assembly.FullName}";

        if (!ExecutedRegisterByAssemblies.ContainsKey(executedRegisterByAssemblyKey))
        {
            action(assembly);

            ExecutedRegisterByAssemblies.TryAdd(executedRegisterByAssemblyKey, assembly);
        }
    }

    protected virtual void InternalRegister(IServiceCollection serviceCollection)
    {
    }

    protected virtual Task InternalInit(IServiceScope serviceScope)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Override this to setup custom value for <see cref="PlatformJsonSerializer.CurrentOptions" />
    /// </summary>
    /// <returns></returns>
    protected virtual JsonSerializerOptions JsonSerializerCurrentOptions()
    {
        return null;
    }

    protected async Task InitAllModuleDependencies()
    {
        await AllDependencyChildModules()
            .GroupBy(p => p.ExecuteInitPriority)
            .OrderByDescending(p => p.Key)
            .ForEachAsync(p => p.ParallelAsync(module => module.Init()));
    }

    protected virtual void RegisterHelpers(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformHelper>(Assembly);
    }

    protected virtual DistributedTracingConfig ConfigDistributedTracing()
    {
        return new DistributedTracingConfig();
    }

    protected void RegisterDefaultLogs(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterIfServiceNotExist(typeof(ILoggerFactory), typeof(LoggerFactory));
        serviceCollection.RegisterIfServiceNotExist(typeof(ILogger<>), typeof(Logger<>));
        serviceCollection.RegisterIfServiceNotExist(typeof(ILogger), IPlatformModule.CreateDefaultLogger);
    }

    protected void RegisterCqrs(IServiceCollection serviceCollection)
    {
        if (AutoScanAssemblyRegisterCqrs)
            ExecuteRegisterByAssemblyOnlyOnce(assembly =>
                {
                    serviceCollection.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

                    serviceCollection.Register<IPlatformCqrs, PlatformCqrs>();
                    serviceCollection.RegisterAllSelfImplementationFromType(typeof(IPipelineBehavior<,>), assembly);
                },
                Assembly,
                nameof(RegisterCqrs));
    }

    protected void RegisterAllModuleDependencies(IServiceCollection serviceCollection)
    {
        ModuleTypeDependencies()
            .Select(moduleTypeProvider => moduleTypeProvider(Configuration))
            .ForEach(moduleType => serviceCollection.RegisterModule(moduleType, true));
    }

    public class DistributedTracingConfig
    {
        public bool Enabled { get; set; }
        public Action<TracerProviderBuilder> AdditionalTraceConfig { get; set; }
        public Action<OtlpExporterOptions> AddOtlpExporterConfig { get; set; }
        public string AppName { get; set; }
    }
}