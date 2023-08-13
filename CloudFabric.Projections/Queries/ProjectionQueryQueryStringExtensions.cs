namespace CloudFabric.Projections.Queries;

public static class ProjectionQueryQueryStringExtensions
{
    /// <summary>
    /// Top-level filters are joined using this character
    /// </summary>
    public const char FILTERS_JOIN_CHARACTER = '!';

    /// <summary>
    /// Individual filter properties are joined using this character.
    /// Example: my_boolean_property|eq|true
    /// </summary>
    public const char FILTER_PROPERTIES_JOIN_CHARACTER = '|';

    /// <summary>
    /// Character used to join filter connector and the filter it's attached to.
    ///
    /// Example: AND+my_boolean_property|eq|true
    /// </summary>
    public const char FILTER_LOGIC_JOIN_CHARACTER = '$';

    /// <summary>
    /// Character used to join array of nested filters.
    /// 
    /// Example: my_boolean_property|eq|true|and$my_int_property|gt|100000000.or$my_string_property|eq|'yo'
    ///          ^- main property filter    ^- start of nester filter array  ^- second filter is joined with .
    ///                                        (joined using | like all props)
    /// </summary>
    public const char FILTER_NESTED_FILTERS_JOIN_CHARACTER = '.';
    
    public static string SerializeToQueryString(
        this ProjectionQuery projectionQuery,
        string? searchText = null,
        int? limit = null,
        int? offset = null,
        Dictionary<string, string>? orderBy = null,
        Filter? filterToAdd = null,
        string? filterTagToRemove = null
    )
    {
        return $"" +
               $"&filters={projectionQuery.SerializeFiltersToQueryString(filterToAdd, filterTagToRemove)}" +
               $"&limit={(limit ?? projectionQuery.Limit)}" +
               $"&offset={(offset ?? projectionQuery.Offset)}" +
               $"&orderBy={projectionQuery.SerializeOrderByToQueryString(orderBy)}" +
               $"&searchText={(string.IsNullOrEmpty(searchText) ? projectionQuery.SearchText : searchText)}";
    }

    public static string SerializeFiltersToQueryString(this ProjectionQuery projectionQuery, Filter? filterToAdd = null, string? filterTagToRemove = null)
    {
        // clone original list since we don't want our modifications to be saved
        var clonedList = projectionQuery.Filters.Select(f => new Filter(f)).ToList();

        if (filterTagToRemove != null)
        {
            clonedList.RemoveAll(f => f.Tag == filterTagToRemove);
        }

        if (filterToAdd != null)
        {
            clonedList.Add(filterToAdd);
        }

        var filtersSerialized = clonedList.Select(f => f.Serialize()).ToList();

        if (filtersSerialized.Count > 0)
        {
            return "sv1_" + string.Join(FILTERS_JOIN_CHARACTER, filtersSerialized);
        }
        else
        {
            return "";
        }
    }

    public static void DeserializeFiltersQueryString(this ProjectionQuery projectionQuery, string filters)
    {
        if (string.IsNullOrEmpty(filters))
        {
            return;
        }

        var searchVersionPlaceholder = "sv";
        var version = "1";

        if (filters.IndexOf(searchVersionPlaceholder) == 0)
        {
            var end = filters.IndexOf("_", searchVersionPlaceholder.Length);
            var versionLength = end - searchVersionPlaceholder.Length;

            version = filters.Substring(searchVersionPlaceholder.Length, versionLength);
            
            // remove version from filters string
            filters = filters.Substring(filters.IndexOf("_") + 1);
        }

        switch (version)
        {
            case "1":
                var filtersList = filters.Split(FILTERS_JOIN_CHARACTER).Where(f => f.Length > 0).ToList();

                if (filtersList.Count > 0)
                {
                    projectionQuery.Filters = filtersList.Select(f => FilterQueryStringExtensions.Deserialize(f)).ToList();
                }

                break;
        }
    }

    public static string SerializeOrderByToQueryString(this ProjectionQuery projectionQuery, Dictionary<string, string> orderBy = null)
    {
        var ordersToWorkWith = orderBy ?? projectionQuery.OrderBy.ToDictionary(k => k.KeyPath, v => v.Order);

        List<string> orders = new List<string>();

        foreach (KeyValuePair<string, string> order in ordersToWorkWith)
        {
            orders.Add($"{order.Key} {order.Value}");
        }

        return string.Join(",", orders);
    }

    public static void DeserializeOrderByQueryString(this ProjectionQuery projectionQuery, string orderByQueryString)
    {
        if (string.IsNullOrEmpty(orderByQueryString))
        {
            return;
        }

        var orders = orderByQueryString.Split(',');

        foreach (var orderBy in orders)
        {
            var orderByParts = orderBy.Split(' ');

            if (orderByParts.Length == 2)
            {
                projectionQuery.OrderBy.Add(
                    new SortInfo { KeyPath = orderByParts[0], Order = orderByParts[1] }
                );
            }
        }
    }
}
