using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using UMS.Platform.Common;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Domain.UnitOfWork;
using UMS.Platform.Infrastructures.BackgroundJob;

namespace UMS.Platform.Application.BackgroundJob;

public interface IPlatformApplicationBackgroundJobExecutor
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformApplicationBackgroundJobExecutor)}");
}

public abstract class PlatformApplicationBackgroundJobExecutor<TParam>
    : PlatformBackgroundJobExecutor<TParam>, IPlatformApplicationBackgroundJobExecutor
    where TParam : class
{
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformApplicationBackgroundJobExecutor(IUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider) : base(loggerFactory, rootServiceProvider)
    {
        UnitOfWorkManager = unitOfWorkManager;
    }

    public virtual bool AutoOpenUow => true;

    protected override async Task InternalExecuteAsync(TParam param = null)
    {
        using (var activity =
               IPlatformApplicationBackgroundJobExecutor.ActivitySource.StartActivity(
                   $"BackgroundJob.{nameof(InternalExecuteAsync)}"))
        {
            activity?.SetTag("Type", GetType().FullName);
            activity?.SetTag("Param", param?.ToJson());

            Logger.LogInformation("[PlatformApplicationBackgroundJobExecutor] {BackgroundJobName} STARTED",
                GetType().Name);

            if (AutoOpenUow)
                using (var uow = UnitOfWorkManager.Begin())
                {
                    await ProcessAsync(param);

                    await uow.CompleteAsync();
                }
            else
                await ProcessAsync(param);

            Logger.LogInformation("[PlatformApplicationBackgroundJobExecutor] {BackgroundJobName} FINISHED",
                GetType().Name);
        }
    }
}

public abstract class PlatformApplicationBackgroundJobExecutor : PlatformApplicationBackgroundJobExecutor<object>
{
    protected PlatformApplicationBackgroundJobExecutor(IUnitOfWorkManager unitOfWorkManager,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider) : base(unitOfWorkManager, loggerFactory, rootServiceProvider)
    {
    }
}