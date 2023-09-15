using UMS.Platform.Common.Logging.BackgroundThreadFullStackTrace;

namespace UMS.Platform.Common.Logging;

/// <summary>
///     Entry Point for using PlatformLogger
/// </summary>
public static class PlatformGlobalLogger
{
    public static IPlatformBackgroundThreadFullStackTraceContextAccessor BackgroundThreadFullStackTraceContextAccessor
    {
        get;
        set;
    } =
        new PlatformBackgroundThreadFullStackTraceContextAccessor();
}