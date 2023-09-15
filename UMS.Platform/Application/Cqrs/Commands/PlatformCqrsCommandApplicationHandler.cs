using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context.UserContext;
using UMS.Platform.Application.Exceptions.Extensions;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs;
using UMS.Platform.Common.Cqrs.Commands;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Utils;
using UMS.Platform.Common.Validations.Extensions;
using UMS.Platform.Domain.UnitOfWork;

namespace UMS.Platform.Application.Cqrs.Commands;

public interface IPlatformCqrsCommandApplicationHandler
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformCqrsCommandApplicationHandler)}");
}

public abstract class PlatformCqrsCommandApplicationHandler<TCommand, TResult>
    : PlatformCqrsRequestApplicationHandler<TCommand>, IRequestHandler<TCommand, TResult>
    where TCommand : PlatformCqrsCommand<TResult>, IPlatformCqrsRequest, new()
    where TResult : PlatformCqrsCommandResult, new()
{
    protected readonly IPlatformCqrs Cqrs;
    protected readonly IUnitOfWorkManager UnitOfWorkManager;

    public PlatformCqrsCommandApplicationHandler(IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider) : base(userContext, loggerFactory, rootServiceProvider)
    {
        UnitOfWorkManager = unitOfWorkManager;
        Cqrs = cqrs;
        IsDistributedTracingEnabled =
            rootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.Enabled == true;
    }

    protected bool IsDistributedTracingEnabled { get; }

    public virtual int FailedRetryCount => 0;

    protected virtual bool AutoOpenUow => true;

    public virtual async Task<TResult> Handle(TCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await HandleWithTracing(request,
                async () =>
                {
                    await ValidateRequestAsync(request.Validate().Of<TCommand>(), cancellationToken).EnsureValidAsync();

                    var result = await Util.TaskRunner.CatchExceptionContinueThrowAsync(
                        () => ExecuteHandleAsync(request, cancellationToken),
                        ex =>
                        {
                            LoggerFactory.CreateLogger(typeof(PlatformCqrsCommandApplicationHandler<>))
                                .Log(ex.IsPlatformLogicException() ? LogLevel.Warning : LogLevel.Error,
                                    ex,
                                    "[{Tag1}] Command:{RequestName} has logic error. AuditTrackId:{AuditTrackId}. Request:{Request}. UserContext:{UserContext}",
                                    ex.IsPlatformLogicException() ? "LogicErrorWarning" : "UnknownError",
                                    request.GetType().Name,
                                    request.AuditInfo.AuditTrackId,
                                    request.ToJson(),
                                    CurrentUser.GetAllKeyValues().ToJson());
                        });

                    await Cqrs.SendEvent(
                        new PlatformCqrsCommandEvent<TCommand>(request, PlatformCqrsCommandEventAction.Executed).With(
                            p => p.SetRequestContextValues(CurrentUser.GetAllKeyValues())),
                        cancellationToken);

                    return result;
                });
        }
        finally
        {
            Util.GarbageCollector.Collect(immediately: false);
        }
    }

    protected async Task<TResult> HandleWithTracing(TCommand request, Func<Task<TResult>> handleFunc)
    {
        if (IsDistributedTracingEnabled)
            using (var activity =
                   IPlatformCqrsCommandApplicationHandler.ActivitySource.StartActivity(
                       $"CommandApplicationHandler.{nameof(Handle)}"))
            {
                activity?.SetTag("RequestType", request.GetType().Name);
                activity?.SetTag("Request", request.ToJson());

                return await handleFunc();
            }

        return await handleFunc();
    }

    protected abstract Task<TResult> HandleAsync(TCommand request, CancellationToken cancellationToken);

    protected virtual async Task<TResult> ExecuteHandleAsync(TCommand request, CancellationToken cancellationToken)
    {
        if (FailedRetryCount > 0)
            return await Util.TaskRunner.WaitRetryThrowFinalExceptionAsync(
                () => DoExecuteHandleAsync(request, cancellationToken),
                retryCount: FailedRetryCount);
        return await DoExecuteHandleAsync(request, cancellationToken);
    }

    protected virtual async Task<TResult> DoExecuteHandleAsync(TCommand request, CancellationToken cancellationToken)
    {
        if (AutoOpenUow == false) return await HandleAsync(request, cancellationToken);

        using (var uow = UnitOfWorkManager.Begin())
        {
            var result = await HandleAsync(request, cancellationToken);

            await uow.CompleteAsync(cancellationToken);

            return result;
        }
    }
}

public abstract class PlatformCqrsCommandApplicationHandler<TCommand>
    : PlatformCqrsCommandApplicationHandler<TCommand, PlatformCqrsCommandResult>
    where TCommand : PlatformCqrsCommand<PlatformCqrsCommandResult>, IPlatformCqrsRequest, new()
{
    public PlatformCqrsCommandApplicationHandler(IPlatformApplicationUserContextAccessor userContext,
        IUnitOfWorkManager unitOfWorkManager,
        IPlatformCqrs cqrs,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider) : base(userContext, unitOfWorkManager, cqrs,
        loggerFactory, rootServiceProvider)
    {
    }

    public abstract Task HandleNoResult(TCommand request, CancellationToken cancellationToken);

    protected override async Task<PlatformCqrsCommandResult> HandleAsync(TCommand request,
        CancellationToken cancellationToken)
    {
        await HandleNoResult(request, cancellationToken);
        return new PlatformCqrsCommandResult();
    }
}