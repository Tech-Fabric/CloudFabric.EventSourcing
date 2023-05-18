namespace CloudFabric.Projections.Queries;

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
    public Dictionary<string, List<string>> Highlights { get; set; } = new ();
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
    public Dictionary<string, List<FacetStats>> FacetsStats { get; set; } = new ();
    public string? IndexName { get; set; }
    public string? QueryId { get; set; }
    public List<QueryResultDocument<T>> Records { get; set; } = new ();
    public long? TotalRecordsFound { get; set; } = 0;

    public string DebugInformation { get; set; }

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