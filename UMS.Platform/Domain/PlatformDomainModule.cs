using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Platform.Common;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Domain.Services;

namespace UMS.Platform.Domain;

public abstract class PlatformDomainModule : PlatformModule
{
    protected PlatformDomainModule(IServiceProvider serviceProvider, IConfiguration configuration) : base(
        serviceProvider,
        configuration)
    {
    }

    protected override void InternalRegister(IServiceCollection serviceCollection)
    {
        base.InternalRegister(serviceCollection);
        serviceCollection.RegisterAllFromType<IPlatformDomainService>(Assembly);
    }
}