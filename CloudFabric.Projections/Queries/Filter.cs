namespace CloudFabric.Projections.Queries;

public class Filter
{
    public List<FilterConnector> Filters = new();

    public Filter()
    {
    }

    /// <summary>
    /// Creates empty filter with specified tag. 
    /// </summary>
    /// <param name="tag"></param>
    public Filter(string tag)
    {
        Tag = tag;
    }

    public Filter(string propertyName, string oper, object? value, string tag = "")
    {
        PropertyName = propertyName;
        Operator = oper;
        Value = value;
        Tag = tag;
    }

    public Filter(Filter filterToClone)
    {
        PropertyName = filterToClone.PropertyName;
        Operator = filterToClone.Operator;
        Value = filterToClone.Value;
        Tag = filterToClone.Tag;

        Filters = filterToClone.Filters.Select(f => new FilterConnector(f)).ToList();
    }

    public string? PropertyName { get; set; }
    public string? Operator { get; set; }
    public object? Value { get; set; }

    /// <summary>
    /// Optional tag - any string used for referencing this particular filter later. Can be useful when serializing to query string.
    /// </summary>
    public string? Tag { get; set; }

    public bool Visible { get; set; } = true;

    public Filter Or(string propertyName, string oper, object value)
    {
        var filter = new Filter(propertyName, oper, value);
        return Or(filter);
    }

    public Filter Or(Filter f)
    {
        var connector = new FilterConnector(FilterLogic.Or, f);
        Filters.Add(connector);
        return this;
    }

    public Filter And(string propertyName, string oper, object value)
    {
        var filter = new Filter(propertyName, oper, value);
        return And(filter);
    }

    public Filter And(Filter f)
    {
        var connector = new FilterConnector(FilterLogic.And, f);
        Filters.Add(connector);
        return this;
    }
}