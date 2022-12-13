namespace CloudFabric.Projections.Models;

public class PagedList<TRecord>
{
    public int TotalCount { get; set; }

    public int Limit { get; set; }
    
    public int Offset { get; set; }

    public List<TRecord> Records { get; set; } = new();
}
