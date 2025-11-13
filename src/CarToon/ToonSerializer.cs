using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        if (IsPrimitiveValue(value))
        {
            SerializePrimitiveValue(value, sb);
            return;
        }

        if (IsCollection(value!))
        {
            SerializeCollection(value!, sb);
            return;
        }
    }

    private static bool IsPrimitiveValue(object? value)
    {
        // TODO: what about Date objects?
        return value is null ||
               value is bool ||
               IsNumeric(value) ||
               value is string;
    }

    private static bool IsCollection(object? value)
    {
        if (value is null)
        {
            return false;
        }

        var type = value.GetType();

        // Check if it implements IEnumerable but not IDictionary or string
        return typeof(IEnumerable).IsAssignableFrom(type) &&
               !typeof(IDictionary).IsAssignableFrom(type) &&
               !(value is string);
    }

    private static bool IsNumeric(object value)
    {
        // Check if the object is any of the fundamental numeric types
        return value is sbyte      // Signed 8-bit integer
               || value is byte    // Unsigned 8-bit integer
               || value is short   // Signed 16-bit integer
               || value is ushort  // Unsigned 16-bit integer
               || value is int     // Signed 32-bit integer
               || value is uint    // Unsigned 32-bit integer
               || value is long    // Signed 64-bit integer
               || value is ulong   // Unsigned 64-bit integer
               || value is float   // Single-precision floating point (System.Single)
               || value is double  // Double-precision floating point (System.Double)
               || value is decimal; // High-precision decimal
    }

    private static void SerializePrimitiveValue(object? value, StringBuilder sb)
    {
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
            SerializeNumericValue(value, sb);
            return;
        }

        if (value is string strValue)
        {
            var canBeUnquoted = CanBeUnquotedString(strValue, documentDelimiter: ToonConstants.DocumentDelimiter);
            SerializeString(strValue, includeQuotes: !canBeUnquoted, sb);
            return;
        }

        throw new InvalidOperationException($"Unsupported primitive type: {value.GetType().FullName}");
    }

    private static void SerializeNumericValue(object obj, StringBuilder sb)
    {
        if (obj is float f && float.IsInfinity(f))
        {
            sb.Append(ToonConstants.NullLiteral);
            return;
        }

        if (obj is double d && double.IsInfinity(d))
        {
            sb.Append(ToonConstants.NullLiteral);
            return;
        }

        if (obj is IFormattable formattable)
        {
            // The 'null' in the first argument means use the default format string
            sb.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
            return;
        }

        sb.Append(obj.ToString() ?? string.Empty);
    }

    private static void SerializeString(string str, bool includeQuotes, StringBuilder sb)
    {
        var escapedString = str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");

        if (includeQuotes)
        {
            sb.Append('"');
            sb.Append(escapedString);
            sb.Append('"');
        }
        else
        {
            sb.Append(escapedString);
        }
    }

    private static bool CanBeUnquotedString(string str, string documentDelimiter, string? activeDelimiter = null)
    {
        if (string.IsNullOrEmpty(str))
        {
            return false;
        }

        // Check if string has leading or trailing whitespace
        if (str.Trim().Length != str.Length)
        {
            return false;
        }

        // Check if string matches reserved literals
        if (str.Equals(ToonConstants.NullLiteral, StringComparison.Ordinal) ||
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

    private static void SerializeCollection(object value, StringBuilder sb)
    {
        var collection = (IEnumerable)value;
        int itemCount = 0;

        foreach (var item in collection)
        {
            itemCount++;
        }

        if (itemCount == 0)
        {
            SerializeArrayHeader(ToonConstants.DefaultItemsKey, 0, sb);
            return;
        }

        if (IsCollectionOfPrimitives(collection))
        {
            SerializeCollectionOfPrimitives(collection, sb);
            return;
        }

        if (IsCollectionOfCollections(collection))
        {
            var allArraysOfPrimitives = true;
            foreach (var item in collection)
            {
                var innerCollection = (IEnumerable)item;
                if (!IsCollectionOfPrimitives(innerCollection))
                {
                    allArraysOfPrimitives = false;
                    break;
                }
            }

            if (allArraysOfPrimitives)
            {
                SerializeArrayHeader(ToonConstants.DefaultItemsKey, itemCount, sb);
                sb.AppendLine();

                foreach (var item in collection)
                {
                    var innerCollection = (IEnumerable)item;
                    sb.Append(GetDefaultIndentation());
                    sb.Append("- ");
                    SerializeCollectionOfPrimitives(innerCollection, sb, string.Empty);
                    sb.AppendLine();
                }
                return;
            }
        }

        if (IsCollectionOfObjects(collection))
        {
            if (TryGetObjectProperties(collection, out var propertyList))
            {
                SerializeArrayHeader(ToonConstants.DefaultItemsKey, itemCount, sb, propertyList);
                sb.AppendLine();

                foreach (var item in collection)
                {
                    var innerCollection = (IEnumerable)item;
                    sb.Append(GetDefaultIndentation());
                    //sb.Append(SerializeObject(innerCollection));
                    sb.AppendLine();
                }
                return;
            }
            else
            {
                throw new InvalidOperationException("Cannot serialize collection: all objects must have the same properties.");
            }
            
           
        }

        throw new NotImplementedException("Serialization of non-primitive collections is not implemented yet.");
    }

    private static bool IsCollectionOfPrimitives(IEnumerable collection)
    {
        foreach (var item in collection)
        {
            if (!IsPrimitiveValue(item))
            {
                return false;
            }
        }
        return true;
    }

    private static void SerializeCollectionOfPrimitives(IEnumerable collection, StringBuilder sb, string key = "items")
    {
        var itemCount = 0;
        foreach (var item in collection)
        {
            itemCount++;
        }

        SerializeArrayHeader(key, itemCount, sb);
        sb.Append(ToonConstants.SingleSpaceLiteral);

        var iterator = 0;
        foreach (var item in collection)
        {
            iterator++;
            SerializePrimitiveValue(item, sb);
            if (iterator < itemCount)
            {
                sb.Append(ToonConstants.DocumentDelimiter);
            }
        }
    }

    private static bool IsCollectionOfCollections(IEnumerable collection)
    {
        foreach (var item in collection)
        {
            if (!IsCollection(item))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsCollectionOfObjects(IEnumerable collection)
    {
        foreach (var item in collection)
        {
            if (item is null || IsPrimitiveValue(item) || IsCollection(item))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetObjectProperties(IEnumerable collection, out string propertyList)
    {
        var propertySignature = string.Empty;
        propertyList = string.Empty;
        var isFirst = true;

        foreach (var item in collection)
        {
            if (item is null)
            {
                return false;
            }

            var type = item.GetType();
            var properties = type.GetProperties();
            
            // Create a signature based on property names and types
            var currentSignature = string.Join("|", properties
                .OrderBy(p => p.Name)
                .Select(p => $"{p.Name}:{p.PropertyType.FullName}"));

            if (isFirst)
            {
                propertySignature = currentSignature;
                propertyList = string.Join(", ", properties
                    .OrderBy(p => p.Name)
                    .Select(p => p.Name));
                isFirst = false;
            }
            else if (propertySignature != currentSignature)
            {
                return false;
            }
        }

        return true;
    }

    private static void SerializeArrayHeader(string key, int length, StringBuilder sb, string? propertyList = null)
    {
        var propertiesHeader = string.IsNullOrWhiteSpace(propertyList) ? string.Empty : $"{propertyList}";
        sb.Append($"{key ?? string.Empty}[{length}]{propertiesHeader}:");
    }

    private static string GetDefaultIndentation(int level = 1)
    {
        return new string(' ', ToonConstants.IndentSize * level);
    }
}