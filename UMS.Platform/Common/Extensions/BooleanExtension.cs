#nullable enable
namespace UMS.Platform.Common.Extensions;

public static class BooleanExtension
{
    public static bool TryParseBooleanOrDefault(this string? boolString)
    {
        return bool.TryParse(boolString, out var parsedValue) && parsedValue;
    }
}