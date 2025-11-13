using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CarToon;

/// <summary>
/// Object serializer into Toon format.
/// </summary>
public static class ToonSerializer
{
    /// <summary>
    /// Serializes the given object into Toon format.
    /// </summary>
    /// <param name="value"></param>
    /// <returns>Toon formatted string representing the given object</returns>
    public static string Serialize(object? value)
    {
        var sb = new StringBuilder();

        SerializeValue(value, sb);

        return sb.ToString();
    }

    private static void SerializeValue(object? value, StringBuilder sb)
    {
        // Serialize primitive types
        if (value is null)
        {
            sb.Append(ToonConstants.NullLiteral);
            return;
        }

        if (value is bool boolValue)
        {
            sb.Append(boolValue ? ToonConstants.TrueLiteral : ToonConstants.FalseLiteral);
            return;
        }

        if (IsNumeric(value))
        {
            sb.Append(SerializeNumericValue(value));
            return;
        }

        if (value is string strValue)
        {
            var canBeUnquoted = CanBeUnquotedString(strValue, documentDelimiter: ToonConstants.DocumentDelimiter);
            sb.Append(SerializeString(strValue, includeQuotes: !canBeUnquoted));
            return;
        }
    }

    private static bool IsNumeric(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        // Check if the object is any of the fundamental numeric types
        return obj is sbyte      // Signed 8-bit integer
               || obj is byte    // Unsigned 8-bit integer
               || obj is short   // Signed 16-bit integer
               || obj is ushort  // Unsigned 16-bit integer
               || obj is int     // Signed 32-bit integer
               || obj is uint    // Unsigned 32-bit integer
               || obj is long    // Signed 64-bit integer
               || obj is ulong   // Unsigned 64-bit integer
               || obj is float   // Single-precision floating point (System.Single)
               || obj is double  // Double-precision floating point (System.Double)
               || obj is decimal; // High-precision decimal
    }

    private static string SerializeNumericValue(object obj)
    {
        if (obj is IFormattable formattable)
        {
            // The 'null' in the first argument means use the default format string
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }
        
        return obj.ToString() ?? string.Empty;
    }

    private static string SerializeString(string str, bool includeQuotes)
    {
        var escapedString = str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");

        return includeQuotes ? $"\"{escapedString}\"" : escapedString;
    }

    private static bool CanBeUnquotedString(string str, string documentDelimiter, string? activeDelimiter = null)
    {
        if (string.IsNullOrEmpty(str))
        {
            return false;
        }

        // Check if string has leading or trailing whitespace
        if(str.Trim().Length != str.Length)
        {
            return false;
        }

        // Check if string matches reserved literals
        if(str.Equals(ToonConstants.NullLiteral, StringComparison.Ordinal) ||
           str.Equals(ToonConstants.TrueLiteral, StringComparison.Ordinal) ||
           str.Equals(ToonConstants.FalseLiteral, StringComparison.Ordinal))
        {
            return false;
        }

        // Check if string is numeric-like
        if (Regex.IsMatch(str, @"^-?\d+(?:\.\d+)?(?:e[+-]?\d+)?$", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(str, @"^0\d+$"))
        {
            return false;
        }

        // Check if string contains colon, double quote, or backslash
        if (str.Contains(":") || str.Contains("\"") || str.Contains("\\"))
        {
            return false;
        }

        // Check if string contains brackets or braces
        if (str.Contains("[") || str.Contains("]") || str.Contains("{") || str.Contains("}"))
        {
            return false;
        }

        // Check if string contains control characters
        if (str.Contains("\n") || str.Contains("\r") || str.Contains("\t"))
        {
            return false;
        }

        // Check if string equals "-" or starts with "-"
        if (str[0] == '-')
        {
            return false;
        }

        // Check if string contains the relevant delimiter
        if (str.Contains(documentDelimiter) || (activeDelimiter != null && str.Contains(activeDelimiter)))
        {
            return false;
        }
        
        return true;
    }
}