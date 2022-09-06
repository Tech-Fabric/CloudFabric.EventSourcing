namespace CloudFabric.Projections;

public class ProjectionDocumentPropertySchema
{
    public string PropertyName { get; set; }
    public TypeCode PropertyType { get; set; }
    public bool IsKey { get; set; } = false;
    public bool IsSearchable { get; set; } = false;
    public bool IsRetrievable { get; set; } = true;
    public string[] SynonymMaps { get; set; } = Array.Empty<string>();
    public double SearchableBoost { get; set; } = 1;
    public bool IsFilterable { get; set; } = false;
    public bool IsSortable { get; set; } = false;
    public bool IsFacetable { get; set; } = false;
    public bool IsNested { get; set; } = false;
    public string? Analyzer { get; set; }
    public string? SearchAnalyzer { get; set; }
    public string? IndexAnalyzer { get; set; }
    public bool UseForSuggestions { get; set; } = false;
    public double[] FacetableRanges { get; set; } = Array.Empty<double>();
}