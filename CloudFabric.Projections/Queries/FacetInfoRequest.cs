namespace CloudFabric.Projections.Queries;

public class FacetInfoRequest
{
    public FacetInfoRequest(string facetName, string sort = "count", int count = 1000, string? sumByField = null)
    {
        FacetName = facetName;
        Sort = sort;
        Count = count;
        SumByField = sumByField;
    }

    /// <summary>
    /// Facetable property name.
    /// </summary>
    public string FacetName { get; set; }

    /// <summary>
    /// How to sort facet results. Count for sorting based on records number.
    /// </summary>
    public string Sort { get; set; }

    /// <summary>
    /// How many facet values to return;
    /// </summary>
    public int Count { get; set; }

    public string? SumByField { get; set; }

    public double[] Values { get; set; } = new double[] { };
}