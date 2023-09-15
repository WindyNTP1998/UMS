using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Platform.Common;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Infrastructures.Abstract;

namespace UMS.Platform.Infrastructures;

public abstract class PlatformInfrastructureModule : PlatformModule
{
    public new const int DefaultExecuteInitPriority =
        PlatformModule.DefaultExecuteInitPriority + ExecuteInitPriorityNextLevelDistance * 3;

    public const int DefaultDependentOnPersistenceInitExecuteInitPriority =
        PlatformModule.DefaultExecuteInitPriority + ExecuteInitPriorityNextLevelDistance * 1;

    public PlatformInfrastructureModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    public override int ExecuteInitPriority => DefaultExecuteInitPriority;

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);

        serviceCollection.RegisterAllFromType<IPlatformInfrastructureService>(Assembly);
    }

    protected override void RegisterHelpers(IServiceCollection serviceCollection)
    {
        serviceCollection.RegisterAllFromType<IPlatformHelper>(typeof(PlatformInfrastructureModule).Assembly);
        serviceCollection.RegisterAllFromType<IPlatformHelper>(Assembly);
    }
}