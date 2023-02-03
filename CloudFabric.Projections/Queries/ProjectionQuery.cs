using System.Linq.Expressions;

namespace CloudFabric.Projections.Queries;

public class ProjectionQuery
{
    public List<FacetInfoRequest> FacetInfoToReturn = new List<FacetInfoRequest>();
    public List<string> FieldsToHighlight = new List<string>();
    // public Dictionary<string, string> OrderBy = new Dictionary<string, string>();
    public List<SortInfo> OrderBy = new();
    public string? ScoringProfile;
    public string? SearchMode;

    // Limit is nullable to have a possibility to retrieve all the records
    public int? Limit { get; set; }
    public int Offset { get; set; } = 0;
    public string SearchText { get; set; } = "*";

    /// <summary>
    /// List of filters. All filters will be joined by AND.
    /// It's handy to have a list because different links may want to remove one filter and add another one.
    /// </summary>
    public List<Filter> Filters { get; set; } = new List<Filter>();

    public static ProjectionQuery Where<T>(Expression<Func<T, bool>> expression)
    {
        var query = new ProjectionQuery();
        query.Filters.Add(Filter.Where<T>(expression));
        return query;
    }

    public Expression<Func<TObject, bool>>? FiltersToExpression<TObject>()
    {
        var parameter = Expression.Parameter(typeof(TObject), "document");
        Expression? expressionToReturn = null;

        var filterExpressions = Filters.Select(f => f.ToLambdaExpression<TObject>(parameter)).ToList();

        if (filterExpressions.Count == 0)
        {
            return null;
        }
        else if (filterExpressions.Count == 1)
        {
            expressionToReturn = filterExpressions[0].Body;
        }
        else if (filterExpressions.Count > 1)
        {
            expressionToReturn = Expression.AndAlso(filterExpressions[0].Body, filterExpressions[1].Body);

            foreach (var filterToAdd in filterExpressions.Skip(2))
            {
                expressionToReturn = Expression.AndAlso(expressionToReturn, filterToAdd.Body);
            }
        }

        return expressionToReturn != null
            ? (Expression<Func<TObject, bool>>)Expression.Lambda(expressionToReturn, parameter)
            : null;
    }

    public string SerializeToQueryString(
        string? searchText = null,
        int? limit = null,
        int? offset = null,
        Dictionary<string, string>? orderBy = null,
        Filter? filterToAdd = null,
        string? filterTagToRemove = null)
    {
        return $"" +
               $"&filters={SerializeFiltersToQueryString(filterToAdd, filterTagToRemove)}" +
               $"&limit={(limit ?? Limit)}" +
               $"&offset={(offset ?? Offset)}" +
               $"&orderBy={SerializeOrderByToQueryString(orderBy)}" +
               $"&searchText={(string.IsNullOrEmpty(searchText) ? SearchText : searchText)}";
    }

    public string SerializeFiltersToQueryString(Filter? filterToAdd = null, string? filterTagToRemove = null)
    {
        // clone original list since we don't want our modifications to be saved
        var clonedList = Filters.Select(f => new Filter(f)).ToList();

        if (filterTagToRemove != null)
        {
            clonedList.RemoveAll(f => f.Tag == filterTagToRemove);
        }

        if (filterToAdd != null)
        {
            clonedList.Add(filterToAdd);
        }

        var serialized = System.Text.Json.JsonSerializer.Serialize(clonedList.Select(f => f.Serialize()));

        serialized = serialized
            .Replace("{", "-_v")
            .Replace("}", "v_-")
            .Replace("[", "-_x")
            .Replace("]", "x_-")
            .Replace(":", "-_i")
            .Replace(",", "-_q");

        return System.Net.WebUtility.UrlEncode(serialized);
    }

    public void DeserializeFiltersQueryString(string filters)
    {
        if (string.IsNullOrEmpty(filters))
        {
            return;
        }

        filters = filters
            .Replace("-_v", "{")
            .Replace("v_-", "}")
            .Replace("-_x", "[")
            .Replace("x_-", "]")
            .Replace("-_i", ":")
            .Replace("-_q", ",");

        List<object> filtersJson =
            System.Text.Json.JsonSerializer.Deserialize<List<object>>(System.Net.WebUtility.UrlDecode(filters));

        this.Filters = filtersJson.Select(s => Filter.Deserialize(s))
            .ToList();
    }

    public string SerializeOrderByToQueryString(Dictionary<string, string>? orderBy = null)
    {
        // NOTE: it doesn't serialize sorting filters to query
        var ordersToWorkWith = orderBy ?? OrderBy.ToDictionary(k => k.KeyPath, v => v.Order);

        List<string> orders = new List<string>();

        foreach (KeyValuePair<string, string> order in ordersToWorkWith)
        {
            orders.Add($"{order.Key} {order.Value}");
        }

        return string.Join(",", orders);
    }

    public void DeserializeOrderByQueryString(string orderByQueryString)
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
                OrderBy.Add(
                    new SortInfo { KeyPath = orderByParts[0], Order = orderByParts[1] }
                );
            }
        }
    }
}