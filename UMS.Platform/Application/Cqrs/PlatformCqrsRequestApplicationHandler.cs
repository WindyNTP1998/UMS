using Microsoft.Extensions.Logging;
using UMS.Platform.Application.Context.UserContext;
using UMS.Platform.Common;
using UMS.Platform.Common.Cqrs;
using UMS.Platform.Common.Validations;

namespace UMS.Platform.Application.Cqrs;

public abstract class PlatformCqrsRequestApplicationHandler<TRequest> : PlatformCqrsRequestHandler<TRequest>
    where TRequest : IPlatformCqrsRequest
{
    protected readonly ILoggerFactory LoggerFactory;
    protected readonly IPlatformRootServiceProvider RootServiceProvider;
    protected readonly IPlatformApplicationUserContextAccessor UserContext;

    public PlatformCqrsRequestApplicationHandler(IPlatformApplicationUserContextAccessor userContext,
        ILoggerFactory loggerFactory,
        IPlatformRootServiceProvider rootServiceProvider)
    {
        UserContext = userContext;
        LoggerFactory = loggerFactory;
        RootServiceProvider = rootServiceProvider;
        Logger = loggerFactory.CreateLogger(typeof(PlatformCqrsRequestApplicationHandler<>));
    }

    public IPlatformApplicationUserContext CurrentUser => UserContext.Current;

    public ILogger Logger { get; }

    public IPlatformCqrsRequestAuditInfo BuildRequestAuditInfo(TRequest request)
    {
        return new PlatformCqrsRequestAuditInfo(Guid.NewGuid(),
            UserContext.Current.UserId());
    }

    /// <summary>
    ///     Override this function to implement additional async validation logic for the request
    /// </summary>
    protected virtual async Task<PlatformValidationResult<TRequest>> ValidateRequestAsync(
        PlatformValidationResult<TRequest> requestSelfValidation,
        CancellationToken cancellationToken)
    {
        return requestSelfValidation;
    }

    protected virtual Task<PlatformValidationResult<TRequest>> ValidateRequestAsync(TRequest request,
        CancellationToken cancellationToken)
    {
        return ValidateRequestAsync(request.Validate().Of<TRequest>(), cancellationToken);
    }
}