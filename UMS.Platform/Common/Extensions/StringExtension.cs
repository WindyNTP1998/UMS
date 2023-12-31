#nullable enable
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Text;
using System.Text.RegularExpressions;
using UMS.Platform.Common.JsonSerialization;

namespace UMS.Platform.Common.Extensions;

public static class StringExtension
{
    public static string TakeTop(this string strValue, int takeMaxLength)
    {
        return strValue.Length >= takeMaxLength ? strValue.Substring(0, takeMaxLength) : strValue;
    }

    public static string EnsureNotNullOrWhiteSpace(this string? target, Func<Exception> exception)
    {
        return target.Ensure(target => !string.IsNullOrWhiteSpace(target), exception)!;
    }

    public static string EnsureNotNullOrEmpty(this string? target, Func<Exception> exception)
    {
        return target.Ensure(target => !string.IsNullOrEmpty(target), exception)!;
    }

    public static T ParseToSerializableType<T>(this string? strValue)
    {
        return strValue != null
            ? PlatformJsonSerializer.Deserialize<T>(PlatformJsonSerializer.Serialize(strValue))
            : default!;
    }

    public static object ParseToSerializableType(this string? strValue, Type serializeType)
    {
        return strValue != null
            ? PlatformJsonSerializer.Deserialize(PlatformJsonSerializer.Serialize(strValue), serializeType)
            : default!;
    }

    public static string SliceFromRight(this string strValue, int fromIndex, int toIndex = 0)
    {
        return strValue.Substring(toIndex, strValue.Length - fromIndex);
    }

    public static bool IsNotNullOrEmpty([NotNullWhen(true)] this string? strValue)
    {
        return !string.IsNullOrEmpty(strValue);
    }

    public static bool IsNotNullOrWhiteSpace([NotNullWhen(true)] this string? strValue)
    {
        return !string.IsNullOrWhiteSpace(strValue);
    }

    public static bool IsNullOrEmpty(this string? strValue)
    {
        return string.IsNullOrEmpty(strValue);
    }

    public static bool IsNullOrWhiteSpace(this string? strValue)
    {
        return string.IsNullOrWhiteSpace(strValue);
    }

    [Pure]
    public static string RemoveSpecialCharactersUri(this string source, string replace = "")
    {
        return Regex.Replace(source, @"[^0-9a-zA-Z\._()-\/]+", replace);
    }

    public static T ParseToEnum<T>(this string enumStringValue) where T : Enum
    {
        return (T)Enum.Parse(typeof(T), enumStringValue);
    }

    public static string Duplicate(this string duplicateStr, int numberOfDuplicateTimes)
    {
        var strBuilder = new StringBuilder();

        for (var i = 0; i <= numberOfDuplicateTimes; i++) strBuilder.Append(duplicateStr);

        return strBuilder.ToString();
    }

    public static string ConcatString(this IEnumerable<char> chars)
    {
        return string.Concat(chars);
    }

    public static string ConcatString(this string prevStr, ReadOnlySpan<char> chars)
    {
        return string.Concat(prevStr.AsSpan(), chars);
    }

    public static string TakeUntilNextChar(this string str, char beforeChar)
    {
        return str.Substring(0, str.IndexOf(beforeChar));
    }

    public static string? ToBase64String(this string? str)
    {
        return str != null ? Convert.ToBase64String(Encoding.UTF8.GetBytes(str)) : null;
    }

    /// <summary>
    ///     Parse value from base64 format to normal utf8 string. <br />
    ///     If fail return the original value.
    /// </summary>
    public static string TryFromBase64ToString(this string str)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(str));
        }
        catch (Exception)
        {
            return str;
        }
    }

    public static bool ContainsIgnoreCase(this string? str, string value)
    {
        return str?.Contains(value, StringComparison.InvariantCultureIgnoreCase) == true;
    }

    public static string ToUniqueStr(this string str)
    {
        return str + " " + Guid.NewGuid();
    }

    public static string ConcatString(this ReadOnlySpan<char> str1, params string[] otherStrings)
    {
        return string.Concat(str1,
            otherStrings.Aggregate((current, next) => string.Concat(current.AsSpan(), next.AsSpan())).AsSpan());
    }
}