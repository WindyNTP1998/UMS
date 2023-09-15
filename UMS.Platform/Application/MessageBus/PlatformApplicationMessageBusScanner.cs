using System.Reflection;
using UMS.Platform.Application.MessageBus.Producers.CqrsEventProducers;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Infrastructures.MessageBus;

namespace UMS.Platform.Application.MessageBus;

public class PlatformApplicationMessageBusScanner : PlatformMessageBusScanner
{
    public PlatformApplicationMessageBusScanner(IServiceProvider serviceProvider) : base(serviceProvider)
    {
    }

    public override List<string> ScanAllDefinedMessageBindingRoutingKeys()
    {
        return base.ScanAllDefinedMessageBindingRoutingKeys()
            .Concat(AllDefaultBindingRoutingKeyForCqrsEventBusMessageProducers().Select(p => p.ToString()))
            .ToList();
    }

    public override List<Assembly> ScanAssemblies()
    {
        return base.ScanAssemblies()
            .ConcatSingle(typeof(PlatformApplicationModule).Assembly)
            .Distinct()
            .ToList();
    }

    public List<PlatformBusMessageRoutingKey> AllDefaultBindingRoutingKeyForCqrsEventBusMessageProducers()
    {
        return ScanAssemblies()
            .SelectMany(p => p.GetTypes())
            .Where(p => p.IsClass && !p.IsAbstract)
            .Select(p => p.FindMatchedGenericType(typeof(PlatformCqrsEventBusMessageProducer<,>)))
            .Where(matchedCqrsEventBusMessageProducerType => matchedCqrsEventBusMessageProducerType != null)
            .Select(cqrsEventBusMessageProducerType =>
                PlatformBusMessageRoutingKey.BuildDefaultRoutingKey(
                    IPlatformCqrsEventBusMessageProducer.GetTMessageArgumentType(cqrsEventBusMessageProducerType)))
            .Distinct()
            .ToList();
    }
}