using UMS.Platform.Common.Validations;
using UMS.Platform.Common.Validations.Exceptions;

namespace UMS.Platform.Domain.Exceptions;

public class PlatformDomainValidationException : PlatformDomainException, IPlatformValidationException
{
    public PlatformDomainValidationException(PlatformValidationResult validationResult) : base(
        validationResult.ToString())
    {
        ValidationResult = validationResult;
    }

    public PlatformValidationResult ValidationResult { get; set; }
}