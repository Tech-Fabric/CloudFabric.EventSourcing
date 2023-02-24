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
}