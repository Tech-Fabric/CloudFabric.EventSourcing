namespace CloudFabric.Projections.Attributes;

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public class ProjectionDocumentPropertyAttribute : Attribute
{
    public virtual bool IsKey { get; set; } = false;
    public virtual bool IsSearchable { get; set; } = false;
    public virtual bool IsRetrievable { get; set; } = true;
    public virtual string[] SynonymMaps { get; set; } = new string[] { };
    public virtual double SearchableBoost { get; set; } = 1;
    public virtual bool IsFilterable { get; set; } = false;
    public virtual bool IsSortable { get; set; } = false;
    public virtual bool IsFacetable { get; set; } = false;
    public virtual string? Analyzer { get; set; }
    public virtual string? SearchAnalyzer { get; set; }
    public virtual string? IndexAnalyzer { get; set; }

    public virtual bool UseForSuggestions { get; set; } = false;

    public virtual double[] FacetableRanges { get; set; } = new double[] { };

    public virtual bool IsNestedObject { get; set; } = false;
    public virtual bool IsNestedArray { get; set; } = false;
}