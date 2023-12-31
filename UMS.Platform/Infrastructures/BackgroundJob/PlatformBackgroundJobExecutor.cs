using Microsoft.Extensions.Logging;
using UMS.Platform.Common;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Utils;

namespace UMS.Platform.Infrastructures.BackgroundJob;

/// <summary>
///     Interface for a background job executor.
/// </summary>
public interface IPlatformBackgroundJobExecutor
{
    /// <summary>
    ///     This method will be executed when processing the job
    /// </summary>
    public void Execute();

    /// <summary>
    ///     Config the time in milliseconds to log warning if the process job time is over ProcessWarningTimeMilliseconds.
    /// </summary>
    public double? SlowProcessWarningTimeMilliseconds();
}

/// <summary>
///     Interface for a background job executor with param
/// </summary>
public interface IPlatformBackgroundJobExecutor<in TParam> : IPlatformBackgroundJobExecutor
{
    /// <summary>
    ///     This method will be executed when processing the job
    /// </summary>
    public void Execute(TParam param);
}

/// <summary>
///     Base class for any background job executor with param. Define a job be extend from this class.
/// </summary>
public abstract class PlatformBackgroundJobExecutor<TParam> : IPlatformBackgroundJobExecutor<TParam>
    where TParam : class
{
    protected readonly ILogger Logger;

    public PlatformBackgroundJobExecutor(ILoggerFactory loggerFactory, IPlatformRootServiceProvider rootServiceProvider)
    {
        RootServiceProvider = rootServiceProvider;
        Logger = loggerFactory.CreateLogger(typeof(PlatformBackgroundJobExecutor));
    }

    protected IPlatformRootServiceProvider RootServiceProvider { get; }

    /// <summary>
    ///     Config the time in milliseconds to log warning if the process job time is over ProcessWarningTimeMilliseconds.
    /// </summary>
    public virtual double? SlowProcessWarningTimeMilliseconds()
    {
        return null;
    }

    public virtual void Execute(TParam param)
    {
        try
        {
            if (SlowProcessWarningTimeMilliseconds() > 0)
            {
                Logger.LogInformation("BackgroundJobExecutor invoking background job {GetTypeFullName} STARTED",
                    GetType().FullName);

                Util.TaskRunner
                    .ProfileExecutionAsync(() => InternalExecuteAsync(param),
                        elapsedMilliseconds =>
                        {
                            var logMessage =
                                $"ElapsedMilliseconds:{elapsedMilliseconds}.";

                            if (elapsedMilliseconds >= SlowProcessWarningTimeMilliseconds())
                                Logger.LogWarning(
                                    "BackgroundJobExecutor invoking background job {GetTypeFullName} FINISHED. SlowProcessWarningTimeMilliseconds:{SlowProcessWarningTimeMilliseconds()}. {LogMessage}",
                                    GetType().FullName,
                                    SlowProcessWarningTimeMilliseconds(),
                                    logMessage);
                            else
                                Logger.LogInformation(
                                    "BackgroundJobExecutor invoking background job {GetTypeFullName} FINISHED. {LogMessage}",
                                    GetType().FullName,
                                    logMessage);
                        })
                    .WaitResult();
            }
            else
            {
                InternalExecuteAsync(param).WaitResult();
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "[BackgroundJob] Job {BackgroundJobType_Name} execution was failed.", GetType().Name);
            throw;
        }
        finally
        {
            Util.GarbageCollector.Collect(immediately: true);
        }
    }

    public virtual void Execute()
    {
        Execute(null);
    }

    public abstract Task ProcessAsync(TParam param = null);

    protected virtual async Task InternalExecuteAsync(TParam param = null)
    {
        await ProcessAsync(param);
    }
}

/// <summary>
///     Base class for any background job executor. Define a job be extend from this class.
/// </summary>
public abstract class PlatformBackgroundJobExecutor : PlatformBackgroundJobExecutor<object>
{
    protected PlatformBackgroundJobExecutor(ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, rootServiceProvider)
    {
    }
}