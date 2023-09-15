using Microsoft.Extensions.DependencyInjection;
using UMS.Platform.Common.Extensions;

namespace UMS.Platform.Common.DependencyInjection;

public static class PlatformRegisterModuleServiceCollectionExtensions
{
    /// <summary>
    ///     Registers a platform module of type 'TModule' into the services collection.
    /// </summary>
    public static IServiceCollection RegisterModule<TModule>(this IServiceCollection services,
        bool isChildModule = false) where TModule : PlatformModule
    {
        return RegisterModule(services, typeof(TModule), isChildModule);
    }

    /// <summary>
    ///     Registers a platform module of type of 'moduleType' param into the services collection.
    /// </summary>
    public static IServiceCollection RegisterModule(this IServiceCollection services,
        Type moduleType,
        bool isChildModule = false)
    {
        if (!moduleType.IsAssignableTo(typeof(PlatformModule)))
            throw new ArgumentException("ModuleType parameter is invalid. It must be inherit from PlatformModule");

        services.Register(typeof(IServiceCollection),
            sp => services,
            ServiceLifeTime.Singleton,
            true,
            DependencyInjectionExtension.CheckRegisteredStrategy.ByService);

        RegisterModuleInstance(services, moduleType);

        var serviceProvider = services.BuildServiceProvider();

        var newRegisterModule = serviceProvider.GetRequiredService(moduleType)
            .As<PlatformModule>()
            .With(_ => _.IsChildModule = isChildModule);

        newRegisterModule.RegisterServices(services);

        serviceProvider
            .GetServices<PlatformModule>()
            .Where(p => !p.GetType().IsAssignableTo(moduleType))
            .ToList()
            .ForEach(otherRegisteredModule =>
                otherRegisteredModule.OnNewOtherModuleRegistered(services, newRegisterModule));

        return services;
    }

    private static void RegisterModuleInstance(IServiceCollection services,
        Type moduleType)
    {
        services.Register(moduleType,
            moduleType,
            ServiceLifeTime.Singleton,
            false,
            skipIfExist: true);

        services.Register(typeof(PlatformModule),
            sp => sp.GetService(moduleType),
            ServiceLifeTime.Singleton,
            false);

        services.RegisterAllForImplementation(moduleType,
            sp => sp.GetService(moduleType),
            ServiceLifeTime.Singleton,
            false);
    }
}