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
    private const string SingleSpaceLiteral = " ";
    private const string DefaultItemsKey = "items";

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
            sb.Append(SerializePrimitiveValue(value));
            return;
        }

        if (IsCollection(value!))
        {
            sb.Append(SerializeCollection(value!));
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

    private static string SerializePrimitiveValue(object? value)
    {
        if (value is null)
        {
            return ToonConstants.NullLiteral;
        }

        if (value is bool boolValue)
        {
            return boolValue ? ToonConstants.TrueLiteral : ToonConstants.FalseLiteral;
        }

        if (IsNumeric(value))
        {
            return SerializeNumericValue(value);
        }

        if (value is string strValue)
        {
            var canBeUnquoted = CanBeUnquotedString(strValue, documentDelimiter: ToonConstants.DocumentDelimiter);
            return SerializeString(strValue, includeQuotes: !canBeUnquoted);
        }

        throw new InvalidOperationException($"Unsupported primitive type: {value.GetType().FullName}");
    }

    private static string SerializeNumericValue(object obj)
    {
        if (obj is float f && float.IsInfinity(f))
        {
            return ToonConstants.NullLiteral;
        }

        if (obj is double d && double.IsInfinity(d))
        {
            return ToonConstants.NullLiteral;
        }

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

    private static string SerializeCollection(object value)
    {
        var collection = (IEnumerable)value;
        int itemCount = 0;

        foreach (var item in collection)
        {
            itemCount++;
        }

        if (itemCount == 0)
        {
            return SerializeArrayHeader(DefaultItemsKey, 0);
        }

        if (IsCollectionOfPrimitives(collection))
        {
            return SerializeCollectionOfPrimitives(collection);
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
                var sb = new StringBuilder();
                sb.AppendLine(SerializeArrayHeader(DefaultItemsKey, itemCount));

                foreach (var item in collection)
                {
                    var innerCollection = (IEnumerable)item;
                    sb.Append(GetDefaultIndentation());
                    sb.Append("- ");
                    sb.Append(SerializeCollectionOfPrimitives(innerCollection, string.Empty));
                    sb.AppendLine();
                }
                return sb.ToString();
            }
        }

        if (IsCollectionOfObjects(collection))
        {
            if (TryGetObjectProperties(collection, out var propertyList))
            {
                 var sb = new StringBuilder();
                sb.AppendLine(SerializeArrayHeader(DefaultItemsKey, itemCount, propertyList));

                foreach (var item in collection)
                {
                    var innerCollection = (IEnumerable)item;
                    sb.Append(GetDefaultIndentation());
                    //sb.Append(SerializeObject(innerCollection));
                    sb.AppendLine();
                }
                return sb.ToString();
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

    private static string SerializeCollectionOfPrimitives(IEnumerable collection, string key = "items")
    {
        var itemCount = 0;
        foreach (var item in collection)
        {
            itemCount++;
        }

        var sb = new StringBuilder();
        sb.Append(SerializeArrayHeader(key, itemCount));
        sb.Append(SingleSpaceLiteral);

        var iterator = 0;
        foreach (var item in collection)
        {
            iterator++;
            sb.Append(SerializePrimitiveValue(item));
            if (iterator < itemCount)
            {
                sb.Append(ToonConstants.DocumentDelimiter);
            }
        }
        return sb.ToString();
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

    private static string SerializeArrayHeader(string key, int length, string? propertyList = null)
    {
        var propertiesHeader = string.IsNullOrWhiteSpace(propertyList) ? string.Empty : $"{propertyList}";
        return $"{key ?? string.Empty}[{length}]{propertiesHeader}:";
    }

    private static string GetDefaultIndentation(int level = 1)
    {
        return new string(' ', ToonConstants.IndentSize * level);
    }
}