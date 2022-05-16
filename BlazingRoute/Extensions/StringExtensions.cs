namespace BlazingRoute.Extensions;

internal static class StringExtensions
{
    public static string ToLowerFirstChar(this string str) =>
        !string.IsNullOrWhiteSpace(str) && char.IsUpper(str![0])
            ? str.Length == 1
                ? char.ToLower(str[0]).ToString()
                : char.ToLower(str[0]) + str[1..]
            : str;
}
