namespace CloudFabric.Projections.Queries;

public static class FilterConnectorQueryStringExtensions
{
    public static string Serialize(this FilterConnector filterConnector)
    {
        return $"{filterConnector.Logic}{ProjectionQueryQueryStringExtensions.FILTER_LOGIC_JOIN_CHARACTER}{filterConnector.Filter.Serialize()}";
    }

    public static FilterConnector Deserialize(string serialized)
    {
        var separator = $"{ProjectionQueryQueryStringExtensions.FILTER_LOGIC_JOIN_CHARACTER}";

        var logicEnd = serialized.IndexOf(separator, StringComparison.Ordinal);

        if(logicEnd < 0)
        {
            separator = "'";
            logicEnd = serialized.IndexOf(separator, StringComparison.Ordinal);
        }

        var filterStart = logicEnd + 1;
        var filterEnd = serialized.LastIndexOf(separator, StringComparison.Ordinal);

        var logic = serialized.Substring(0, logicEnd);
        var filter = serialized.Substring(filterStart);

        var fc = new FilterConnector(logic, FilterQueryStringExtensions.Deserialize(filter));
        
        return fc;
    }
}
