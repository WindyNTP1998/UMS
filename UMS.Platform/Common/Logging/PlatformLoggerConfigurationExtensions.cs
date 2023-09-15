using Serilog;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Logging.BackgroundThreadFullStackTrace;

namespace UMS.Platform.Common.Logging;

public static class PlatformLoggerConfigurationExtensions
{
    public static LoggerConfiguration EnrichDefaultPlatformEnrichers(this LoggerConfiguration loggerConfiguration)
    {
        return loggerConfiguration.Enrich.With(new PlatformBackgroundThreadFullStackTraceEnricher());
    }

    public static LoggerConfiguration WithExceptionDetails(this LoggerConfiguration loggerConfiguration,
        Action<DestructuringOptionsBuilder> configDestructurers = null)
    {
        return loggerConfiguration.Enrich.WithExceptionDetails(new DestructuringOptionsBuilder()
            .WithDefaultDestructurers()
            .WithIf(configDestructurers != null, _ => configDestructurers?.Invoke(_)));
    }
}