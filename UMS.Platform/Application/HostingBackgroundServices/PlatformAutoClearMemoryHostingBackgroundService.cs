using Microsoft.Extensions.Logging;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Hosting;
using UMS.Platform.Common.Utils;

namespace UMS.Platform.Application.HostingBackgroundServices;

internal sealed class PlatformAutoClearMemoryHostingBackgroundService : PlatformIntervalProcessHostedService
{
    public PlatformAutoClearMemoryHostingBackgroundService(IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory) : base(serviceProvider, loggerFactory)
    {
    }

    public override bool AutoCleanMemory => false;

    protected override TimeSpan ProcessTriggerIntervalTime()
    {
        return 3.Seconds();
    }

    protected override async Task IntervalProcessAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() =>
            {
                GC.Collect();
                Util.GarbageCollector.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true,
                    true, immediately: true);
            },
            cancellationToken);
    }
}