using UMS.Platform.Common.Validations;

namespace UMS.Platform.Common.Dtos;

public interface IPlatformDto
{
}

public interface IPlatformDto<TDto> : IPlatformDto
    where TDto : IPlatformDto<TDto>
{
    PlatformValidationResult<TDto> Validate();
}

public interface IPlatformDto<TDto, out TMapForObject> : IPlatformDto<TDto>
    where TDto : IPlatformDto<TDto>
    where TMapForObject : class
{
    TMapForObject MapToObject();
}