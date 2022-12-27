namespace CloudFabric.Projections;

public class FacetStats
{
    public object? Value { get; set; }
    public long? Count { get; set; }
    public double? From { get; set; }
    public double? To { get; set; }
    public string? SumByField { get; set; }
    public double? SumByValue { get; set; }
}

public class QueryResultDocument<T>
{
    public double Score { get; set; }
    public Dictionary<string, List<string>> Highlights { get; set; } = new Dictionary<string, List<string>>();
    public T? Document { get; set; }

    public QueryResultDocument<TNew> TransformResultDocument<TNew>(Func<T, TNew?> transformFunction)
    {
        return new QueryResultDocument<TNew>()
        {
            Score = Score,
            Highlights = Highlights,
            Document = Document != null ? transformFunction(Document) : default(TNew)
        };
    }

    public string? GetHighlightedTextForField(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName) || !Highlights.ContainsKey(fieldName) || Highlights[fieldName].Count < 1)
        {
            return null;
        }

        return Highlights[fieldName][0];
    }
}

public class ProjectionQueryResult<T>
{
    public Dictionary<string, List<FacetStats>> FacetsStats = new Dictionary<string, List<FacetStats>>();
    public string? IndexName;
    public string? QueryId;
    public List<QueryResultDocument<T>> Records = new List<QueryResultDocument<T>>();
    public long? TotalRecordsFound = 0;

    public ProjectionQueryResult<TNew> TransformResultDocuments<TNew>(Func<T, TNew> transformFunction)
    {
        var result = new ProjectionQueryResult<TNew>();
        result.FacetsStats = FacetsStats;
        result.IndexName = IndexName;
        result.QueryId = QueryId;
        result.TotalRecordsFound = TotalRecordsFound;
        result.Records = Records.Select(r => r.TransformResultDocument<TNew>(transformFunction)).ToList();
        return result;
    }
}