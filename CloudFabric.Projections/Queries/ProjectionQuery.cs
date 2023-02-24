namespace CloudFabric.Projections.Queries;

public class ProjectionQuery
{
    public List<FacetInfoRequest> FacetInfoToReturn = new List<FacetInfoRequest>();
    public List<string> FieldsToHighlight = new List<string>();
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
}
