using System.Reflection;
using CloudFabric.Projections.Attributes;

namespace CloudFabric.Projections;

public static class ProjectionDocumentSchemaFactory
{
    public static ProjectionDocumentSchema FromTypeWithAttributes<T>()
    {
        var schema = new ProjectionDocumentSchema();

        schema.SchemaName = ProjectionDocumentAttribute.GetIndexName<T>();
        schema.Properties = ProjectionDocumentAttribute.GetAllProjectionProperties<T>()
            .Select(p => GetPropertySchema(p.Key, p.Value.DocumentPropertyAttribute, p.Value.NestedDictionary))
            .ToList();

        return schema;
    }

    private static ProjectionDocumentPropertySchema GetPropertySchema(
        PropertyInfo propertyInfo,
        ProjectionDocumentPropertyAttribute documentPropertyAttribute,
        object nestedPropertiesDictionary
    )
    {
        return new ProjectionDocumentPropertySchema()
        {
            PropertyName = propertyInfo.Name,
            PropertyType = ProjectionDocumentAttribute.GetPropertyTypeCode(propertyInfo),
            IsKey = documentPropertyAttribute.IsKey,
            IsSearchable = documentPropertyAttribute.IsSearchable,
            IsRetrievable = documentPropertyAttribute.IsRetrievable,
            SynonymMaps = documentPropertyAttribute.SynonymMaps,
            SearchableBoost = documentPropertyAttribute.SearchableBoost,
            IsFilterable = documentPropertyAttribute.IsFilterable,
            IsSortable = documentPropertyAttribute.IsSortable,
            IsFacetable = documentPropertyAttribute.IsFacetable,
            Analyzer = documentPropertyAttribute.Analyzer,
            SearchAnalyzer = documentPropertyAttribute.SearchAnalyzer,
            IndexAnalyzer = documentPropertyAttribute.IndexAnalyzer,
            UseForSuggestions = documentPropertyAttribute.UseForSuggestions,
            FacetableRanges = documentPropertyAttribute.FacetableRanges,
            IsNestedObject = documentPropertyAttribute.IsNestedObject,
            IsNestedArray = documentPropertyAttribute.IsNestedArray,
            ArrayElementType = documentPropertyAttribute.IsNestedArray
                    ? Type.GetTypeCode(propertyInfo.PropertyType.GenericTypeArguments[0])
                    : null,
            NestedObjectProperties = (documentPropertyAttribute.IsNestedObject || documentPropertyAttribute.IsNestedArray)
                    ? GetNestedObjectProperties(nestedPropertiesDictionary as Dictionary<PropertyInfo, (ProjectionDocumentPropertyAttribute, object)>)
                    : null
        };
    }

    private static List<ProjectionDocumentPropertySchema> GetNestedObjectProperties(Dictionary<PropertyInfo, (ProjectionDocumentPropertyAttribute DocumentPropertyAttribute, object NestedDictionary)> nestedProperties)
    {
        return nestedProperties
            .Select(property => GetPropertySchema(property.Key, property.Value.DocumentPropertyAttribute, property.Value.NestedDictionary))
            .ToList();
    }
}