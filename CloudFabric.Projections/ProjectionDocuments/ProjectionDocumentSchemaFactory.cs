using CloudFabric.Projections.Attributes;

namespace CloudFabric.Projections;

public class ProjectionDocumentSchemaFactory
{
    public static ProjectionDocumentSchema FromTypeWithAttributes<T>()
    {
        var schema = new ProjectionDocumentSchema();

        schema.SchemaName = ProjectionDocumentAttribute.GetIndexName<T>();
        schema.Properties = ProjectionDocumentAttribute.GetAllProjectionProperties<T>().Select(p =>
            new ProjectionDocumentPropertySchema()
            {
                PropertyName = p.Key.Name,
                PropertyType = ProjectionDocumentAttribute.GetPropertyTypeCode(p.Key, typeof(T)),
                IsKey = p.Value.IsKey,
                IsSearchable = p.Value.IsSearchable,
                IsRetrievable = p.Value.IsRetrievable,
                SynonymMaps = p.Value.SynonymMaps,
                SearchableBoost = p.Value.SearchableBoost,
                IsFilterable = p.Value.IsFilterable,
                IsSortable = p.Value.IsSortable,
                IsFacetable = p.Value.IsFacetable,
                IsNested = p.Value.IsNested,
                Analyzer = p.Value.Analyzer,
                SearchAnalyzer = p.Value.SearchAnalyzer,
                IndexAnalyzer = p.Value.IndexAnalyzer,
                UseForSuggestions = p.Value.UseForSuggestions,
                FacetableRanges = p.Value.FacetableRanges,
            }).ToList();

        return schema;
    }
}