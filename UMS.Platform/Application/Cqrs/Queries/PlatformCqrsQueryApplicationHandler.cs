using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context.UserContext;
using UMS.Platform.Application.Cqrs.Commands;
using UMS.Platform.Application.Exceptions.Extensions;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs;
using UMS.Platform.Common.Cqrs.Queries;
using UMS.Platform.Common.Extensions;
using UMS.Platform.Common.Utils;
using UMS.Platform.Common.Validations.Extensions;
using UMS.Platform.Infrastructures.Caching;

namespace UMS.Platform.Application.Cqrs.Queries;

public interface IPlatformCqrsQueryApplicationHandler
{
    public static readonly ActivitySource ActivitySource = new($"{nameof(IPlatformCqrsQueryApplicationHandler)}");
}

public abstract class PlatformCqrsQueryApplicationHandler<TQuery, TResult>
    : PlatformCqrsRequestApplicationHandler<TQuery>, IPlatformCqrsQueryApplicationHandler,
        IRequestHandler<TQuery, TResult>
    where TQuery : PlatformCqrsQuery<TResult>, IPlatformCqrsRequest
{
    protected readonly IPlatformCacheRepositoryProvider CacheRepositoryProvider;

    public PlatformCqrsQueryApplicationHandler(IPlatformApplicationUserContextAccessor userContext,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider,
        IPlatformCacheRepositoryProvider cacheRepositoryProvider) : base(userContext, loggerFactory,
        rootServiceProvider)
    {
        CacheRepositoryProvider = cacheRepositoryProvider;
        IsDistributedTracingEnabled =
            rootServiceProvider.GetService<PlatformModule.DistributedTracingConfig>()?.Enabled == true;
    }

    protected bool IsDistributedTracingEnabled { get; }

    public async Task<TResult> Handle(TQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await HandleWithTracing(request,
                async () =>
                {
                    request.SetAuditInfo<TQuery>(BuildRequestAuditInfo(request));

                    await ValidateRequestAsync(request.Validate().Of<TQuery>(), cancellationToken).EnsureValidAsync();

                    var result = await Util.TaskRunner.CatchExceptionContinueThrowAsync(
                        () => HandleAsync(request, cancellationToken),
                        ex =>
                        {
                            LoggerFactory.CreateLogger(typeof(PlatformCqrsQueryApplicationHandler<,>))
                                .Log(ex.IsPlatformLogicException() ? LogLevel.Warning : LogLevel.Error,
                                    ex,
                                    "[{Tag1}] Query:{RequestName} has logic error. AuditTrackId:{AuditTrackId}. Request:{Request}. UserContext:{UserContext}",
                                    ex.IsPlatformLogicException() ? "LogicErrorWarning" : "UnknownError",
                                    request.GetType().Name,
                                    request.AuditInfo.AuditTrackId,
                                    request.ToJson(),
                                    CurrentUser.GetAllKeyValues().ToJson());
                        });

                    return result;
                });
        }
        finally
        {
            Util.GarbageCollector.Collect(immediately: false);
        }
    }

    protected async Task<TResult> HandleWithTracing(TQuery request, Func<Task<TResult>> handleFunc)
    {
        if (IsDistributedTracingEnabled)
            using (var activity =
                   IPlatformCqrsCommandApplicationHandler.ActivitySource.StartActivity(
                       $"QueryApplicationHandler.{nameof(Handle)}"))
            {
                activity?.SetTag("RequestType", request.GetType().Name);
                activity?.SetTag("Request", request.ToJson());

                return await handleFunc();
            }

        return await handleFunc();
    }

    protected abstract Task<TResult> HandleAsync(TQuery request, CancellationToken cancellationToken);
}