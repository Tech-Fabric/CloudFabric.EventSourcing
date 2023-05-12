namespace CloudFabric.Projections;

public class ProjectionDocumentPropertySchema
{
    public string PropertyName { get; set; }
    public TypeCode PropertyType { get; set; }
    public bool IsKey { get; set; } = false;
    public bool IsSearchable { get; set; } = true;
    public bool IsRetrievable { get; set; } = true;
    public string[] SynonymMaps { get; set; } = Array.Empty<string>();
    public double SearchableBoost { get; set; } = 1;
    public bool IsFilterable { get; set; } = false;
    public bool IsSortable { get; set; } = false;
    public bool IsFacetable { get; set; } = false;
    public string? Analyzer { get; set; }
    public string? SearchAnalyzer { get; set; }
    public string? IndexAnalyzer { get; set; }
    public bool UseForSuggestions { get; set; } = false;
    public double[] FacetableRanges { get; set; } = Array.Empty<double>();

    public bool IsNestedObject { get; set; } = false;
    public bool IsNestedArray { get; set; } = false;
    public TypeCode? ArrayElementType { get; set; }
    // nested object/array properties schema
    public List<ProjectionDocumentPropertySchema> NestedObjectProperties { get; set; }


    public override string ToString()
    {
        return $"ProjectionDocumentPropertySchema:{PropertyName}: {PropertyType}";
    }
}