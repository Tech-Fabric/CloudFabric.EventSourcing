namespace CloudFabric.Projections.Queries;

public static class FilterQueryStringExtensions
{
    public static Dictionary<string, Filter> FacetFilterShortcuts = new Dictionary<string, Filter>()
    {
    };

    public static Dictionary<string, string> FacetValuesShortcuts = new Dictionary<string, string>()
    {
    };

    public static string DesanitizeValue(string value)
    {
        return System.Net.WebUtility.UrlDecode(value)
            .Replace(";dot;", ".")
            .Replace(";amp;", "&")
            .Replace(";excl;", "!")
            .Replace(";dollar;", "$")
            .Replace(";aps;", "'");
    }

    public static string SanitizeValue(string value)
    {
        return value.Replace(".", ";dot;")
            .Replace("&", ";amp;")
            .Replace("!", ";excl;")
            .Replace("$", ";dollar;")
            .Replace("'", ";aps;");
    }

    public static string Serialize(this Filter filter, bool useShortcuts = true)
    {
        if (useShortcuts && filter.Filters.Count == 0)
        {
            var shortcut = FacetFilterShortcuts.FirstOrDefault(f => f.Value.Tag == filter.Tag);
            if (shortcut.Value != null)
            {
                return shortcut.Key;
            }
        }

        var valueSerialized = "";

        if (filter.Value != null)
        {
            valueSerialized = filter.Value.ToString();

            if (FacetValuesShortcuts.ContainsKey(valueSerialized))
            {
                valueSerialized = FacetValuesShortcuts[valueSerialized];
            }

            // replace special characters used for serialization
            valueSerialized = SanitizeValue(valueSerialized);

            if (filter.Value is string)
            {
                valueSerialized = $"'{valueSerialized}'";
            }
        }

        var filtersSerialized = "";

        if (filter.Filters != null && filter.Filters.Count > 0)
        {
            filtersSerialized = string.Join(".", filter.Filters.Select(f => f.Serialize()));
        }

        return $"{(string.IsNullOrEmpty(filter.PropertyName) ? "*" : SanitizeValue(filter.PropertyName))}" +
               $"|{(string.IsNullOrEmpty(filter.Operator) ? "*" : filter.Operator)}" +
               $"|{System.Net.WebUtility.UrlEncode(valueSerialized)}" +
               $"|{(filter.Visible ? 'T' : 'F')}" +
               $"|{System.Net.WebUtility.UrlEncode(filter.Tag)}" +
               $"|{filtersSerialized}";
    }

    public static Filter Deserialize(string f)
    {
        if (f.IndexOf("|", StringComparison.Ordinal) < 0)
        {
            if (FacetFilterShortcuts.ContainsKey(f))
            {
                return FacetFilterShortcuts[f];
            }
        }

        var propertyNameEnd = f.IndexOf("|", StringComparison.Ordinal);
        var propertyName = DesanitizeValue(f.Substring(0, propertyNameEnd));

        var operatorEnd = f.IndexOf("|", propertyNameEnd + 1, StringComparison.Ordinal);
        var operatorValue = f.Substring(propertyNameEnd + 1, operatorEnd - propertyNameEnd - 1);

        var valueEnd = f.IndexOf("|", operatorEnd + 1, StringComparison.Ordinal);
        var value = f.Substring(operatorEnd + 1, valueEnd - operatorEnd - 1);

        var visibleEnd = f.IndexOf("|", valueEnd + 1, StringComparison.Ordinal);
        var visible = f.Substring(valueEnd + 1, visibleEnd - valueEnd - 1) == "T";

        var tagEnd = f.IndexOf("|", visibleEnd + 1, StringComparison.Ordinal);
        var tag = f.Substring(visibleEnd + 1, tagEnd - visibleEnd - 1);

        tag = System.Net.WebUtility.UrlDecode(tag);

        // replace back special serialization characters
        value = DesanitizeValue(value);

        var valueByShortcut = FacetValuesShortcuts.FirstOrDefault(v => v.Value == value);
        if (!string.IsNullOrEmpty(valueByShortcut.Key))
        {
            value = valueByShortcut.Key;
        }
        else if (!string.IsNullOrEmpty(value) && value.Length > 2 && value[0] == '#' && value[value.Length - 1] == '#')
        {
            valueByShortcut = FacetValuesShortcuts
                .FirstOrDefault(v => v.Value == $"({value.Replace("#", "")})");

            if (!string.IsNullOrEmpty(valueByShortcut.Key))
            {
                value = valueByShortcut.Key;
            }
        }

        var filters = new List<FilterConnector>();

        var filtersSerializedList = f.Substring(tagEnd + 1).Split('.');
        if (filtersSerializedList.Length > 0)
        {
            filters = filtersSerializedList
                .Where(fs => fs.Length > 0)
                .Select(FilterConnectorQueryStringExtensions.Deserialize)
                .ToList();
        }

        var filter = new Filter()
        {
            PropertyName = propertyName,
            Operator = operatorValue,
            Tag = tag,
            Visible = visible,
            Filters = filters
        };

        if (value.IndexOf("'", StringComparison.Ordinal) == 0)
        {
            filter.Value = value.Replace("'", "");
        }
        else
        {
            if (bool.TryParse(value, out var boolValue))
            {
                filter.Value = boolValue;
            }
            else if (Int64.TryParse(value, out var longValue))
            {
                filter.Value = longValue;
            }
            else if (Int32.TryParse(value, out var intValue))
            {
                filter.Value = intValue;
            }
            else if (decimal.TryParse(value, out var decimalValue))
            {
                filter.Value = decimalValue;
            }
            else if (DateTime.TryParse(value, out var dateTimeValue))
            {
                filter.Value = dateTimeValue;
            }
            // Important: Guids may be stored via two ways: as simple strings or as Guid objects.
            // If we are here - that means guid was passed as an object, not as a string (see first `if` above).
            // So, if one would want to filter by string guid - they would send it in quotes ''.
            else if (Guid.TryParse(value, out var guidValue))
            {
                filter.Value = guidValue;
            }
        }

        return filter;
    }
}
