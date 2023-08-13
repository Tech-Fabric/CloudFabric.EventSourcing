using CloudFabric.Projections.ElasticSearch.Extensions;
using CloudFabric.Projections.Queries;
using Nest;
using Filter = CloudFabric.Projections.Queries.Filter;

namespace CloudFabric.Projections.ElasticSearch.Helpers;

public static class ElasticSearchFilterFactory
{
    public static List<QueryContainer> ConstructFilters(List<Queries.Filter> filters)
    {
        var filterStrings = new List<string>();

        // create 1st-layer filters
        foreach (var f in filters)
        {
            var conditionFilter = $"({ConstructConditionFilter(f)})";
            var propName = f.PropertyName ?? f.Filters[0].Filter.PropertyName;

            if (propName != null && propName.IndexOf(".", StringComparison.Ordinal) == -1)
            {
                filterStrings.Add(conditionFilter);
            }
        }

        var filter = new List<QueryContainer>()
        {
            new QueryStringQuery()
            {
                Query = string.Join(" AND ", filterStrings),
                AllowLeadingWildcard = true,
                AnalyzeWildcard = true
            }
        };

        // create nested filters
        var nestedQueryStrings = ConstructNestedQueryFilters(filters);

        foreach (var entry in nestedQueryStrings)
        {
            var nestedFilter = new NestedQuery()
            {
                Path = entry.Key,
                Query = new BoolQuery()
                {
                    Filter = new List<QueryContainer>()
                    {
                        new QueryStringQuery()
                        {
                            Query = entry.Value,
                            AllowLeadingWildcard = true,
                            AnalyzeWildcard = true
                        }
                    }
                }
            };

            filter.Add(nestedFilter);
        }

        return filter;
    }

    private static Dictionary<string, string> ConstructNestedQueryFilters(List<Queries.Filter> filters)
    {
        var result = new Dictionary<string, string>();

        if (filters == null || filters.Count == 0)
        {
            return result;
        }

        var nestedFiltersStrings = new Dictionary<string, List<string>>();

        foreach (var f in filters)
        {
            var propName = f.PropertyName == null ? f.Filters[0].Filter.PropertyName : f.PropertyName;
            var pathParts = propName.Split('.');

            if (pathParts.Count() <= 1)
            {
                continue;
            }

            var conditionFilter = $"({ConstructConditionFilter(f)})";
            var nestedPath = string.Join(".", pathParts.Take(pathParts.Length - 1));

            if (!nestedFiltersStrings.ContainsKey(nestedPath))
            {
                nestedFiltersStrings[nestedPath] = new List<string>();
            }

            nestedFiltersStrings[nestedPath].Add(conditionFilter);
        }

        foreach (var entry in nestedFiltersStrings)
        {
            result[entry.Key] = string.Join(" AND ", entry.Value);
        }

        return result;
    }

    private static string ConstructConditionFilter(Queries.Filter filter)
    {
        var q = ConstructOneConditionFilter(filter);

        foreach (FilterConnector f in filter.Filters)
        {
            if (!string.IsNullOrEmpty(q) && f.Logic != null)
            {
                q += $" {f.Logic.ToUpper()} ";
            }

            var wrapWithParentheses = f.Logic != null;

            if (wrapWithParentheses)
            {
                q += "(";
            }

            q += ConstructConditionFilter(f.Filter);

            if (wrapWithParentheses)
            {
                q += ")";
            }
        }

        return q;
    }

    private static string ConstructOneConditionFilter(Queries.Filter filter)
    {
        if (string.IsNullOrEmpty(filter.PropertyName) || filter.PropertyName == "*")
        {
            return "";
        }

        if (filter.Value is DateTime || filter.Value is DateTime?)
        {
            return ConstructDateTimeOneConditionFilter(filter);
        }

        var propertyName = filter.PropertyName;
        var filterOperator = "";
        var filterValue = filter.Value?.ToString()?.EscapeElasticUnsupportedCharacters();

        if (filter.Value is bool)
        {
            filterValue = filterValue.ToLower();
        }

        switch (filter.Operator)
        {
            case FilterOperator.ArrayContains:
                filterOperator = ":";
                filterValue = $"{filterValue}";
                break;
            case FilterOperator.Contains:
                filterOperator = ":";
                filterValue = $"*{filterValue}*";
                break;
            case FilterOperator.ContainsIgnoreCase:
                filterOperator = ":";
                filterValue = $"*{filterValue}*";
                propertyName += ".case-insensitive";
                break;
            case FilterOperator.StartsWith:
                filterOperator = ":";
                filterValue = $"{filterValue}*";
                break;
            case FilterOperator.StartsWithIgnoreCase:
                filterOperator = ":";
                filterValue = $"{filterValue}*";
                propertyName += ".case-insensitive";
                break;
            case FilterOperator.EndsWith:
                filterOperator = ":";
                filterValue = $"*{filterValue}";
                break;
            case FilterOperator.EndsWithIgnoreCase:
                filterOperator = ":";
                filterValue = $"*{filterValue}";
                propertyName += ".case-insensitive";
                break;
            case FilterOperator.NotEqual:
            case FilterOperator.Equal:
                filterOperator = ":";
                if (filter.Value is string)
                {
                    // for filter conditions we need exact match
                    filterValue = $"\"{filterValue}\"";
                }
                break;
            case FilterOperator.Greater:
                filterOperator = ":>";
                break;
            case FilterOperator.GreaterOrEqual:
                filterOperator = ":>=";
                break;
            case FilterOperator.Lower:
                filterOperator = ":<";
                break;
            case FilterOperator.LowerOrEqual:
                filterOperator = ":<=";
                break;
        }

        var condition = $"{propertyName}{filterOperator}{filterValue}";
        if (filter.Value == null)
        {
            if (filter.Operator == FilterOperator.NotEqual)
            {
                return $"(_exists_:{propertyName})";
            } 
            else if (filter.Operator == FilterOperator.Equal)
            {
                return $"(!(_exists_:{propertyName}))";
            }
            else
            {
                throw new ArgumentException("Comparing to null should only be via equal or not equal operators.");
            }
        }
        else if (filter.Operator == FilterOperator.NotEqual)
        {
            return $"(!({condition}))";
        }

        return condition;
    }

    private static string ConstructDateTimeOneConditionFilter(Queries.Filter filter)
    {
        if (filter.Value == null)
        {
            return $"({filter.PropertyName}:null OR (!(_exists_:{filter.PropertyName})))";
        }

        var filterValue = ((DateTime)filter.Value).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        switch (filter.Operator)
        {
            case FilterOperator.NotEqual:
            case FilterOperator.Equal:
                filterValue = $"[{filterValue} TO {filterValue}]";
                break;
            case FilterOperator.Greater:
            case FilterOperator.GreaterOrEqual:
                filterValue = $"[{filterValue} TO *]";
                break;
            case FilterOperator.Lower:
            case FilterOperator.LowerOrEqual:
                filterValue = $"[* TO {filterValue}]";
                break;
        }

        var condition = $"{filter.PropertyName}:{filterValue}";

        if (filter.Operator == FilterOperator.NotEqual)
        {
            return $"!({condition})";
        }

        return condition;
    }
}