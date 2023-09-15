using UMS.Platform.Common.Exceptions;
using UMS.Platform.Common.Validations.Exceptions;
using UMS.Platform.Domain.Exceptions;

namespace UMS.Platform.Application.Exceptions.Extensions;

public static class GeneralPlatformLogicExceptionExtension
{
    public static bool IsPlatformLogicException(this Exception ex)
    {
        return ex is PlatformPermissionException or
            PlatformNotFoundException or
            PlatformApplicationException or
            PlatformDomainException or
            IPlatformValidationException;
    }
}