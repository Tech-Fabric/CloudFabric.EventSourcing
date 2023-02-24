namespace CloudFabric.Projections.Queries;

public static class ProjectionQueryQueryStringExtensions
{
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
            return "sv1_" + string.Join("!", filtersSerialized);
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
        var version = "";

        if (filters.IndexOf(searchVersionPlaceholder) == 0)
        {
            var end = filters.IndexOf("_", searchVersionPlaceholder.Length);
            var versionLength = end - searchVersionPlaceholder.Length;

            version = filters.Substring(searchVersionPlaceholder.Length, versionLength);
        }

        switch (version)
        {
            case "1":
                // remove version from filters string
                filters = filters.Substring(filters.IndexOf("_") + 1);
                var filtersList = filters.Split('!').Where(f => f.Length > 0).ToList();

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
