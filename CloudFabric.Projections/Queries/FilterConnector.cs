namespace CloudFabric.Projections.Queries;

public class FilterConnector
{
    public FilterConnector(string logic, Filter filter)
    {
        Logic = logic;
        Filter = filter;
    }

    public FilterConnector(FilterConnector connectorToClone)
    {
        Logic = connectorToClone.Logic;
        Filter = new Filter(connectorToClone.Filter);
    }

    /// <summary>
    /// Logical operator which connects this filter to another filter;
    /// </summary>
    public string Logic { get; init; }

    public Filter Filter { get; init; }

    public object Serialize()
    {
        var obj = new
        {
            l = Logic,
            f = Filter.Serialize()
        };

        return obj;
    }

    public static FilterConnector Deserialize(dynamic obj)
    {
        var fc = new FilterConnector(obj.l, Projections.Queries.Filter.Deserialize(obj.f));
        return fc;
    }
}