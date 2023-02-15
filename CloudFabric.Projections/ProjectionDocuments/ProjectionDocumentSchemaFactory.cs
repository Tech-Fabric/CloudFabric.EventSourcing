using System.Reflection;
using System.Text;
using CloudFabric.Projections.Attributes;

namespace CloudFabric.Projections;

public static class ProjectionDocumentSchemaFactory
{
    public static ProjectionDocumentSchema FromTypeWithAttributes<T>()
    {
        var schema = new ProjectionDocumentSchema();
        
        schema.Properties = ProjectionDocumentAttribute.GetAllProjectionProperties<T>()
            .Select(p => GetPropertySchema(p.Key, p.Value.DocumentPropertyAttribute, p.Value.NestedDictionary))
            .ToList();

        schema.SchemaName = $"{typeof(T).Name}_{GetPropertiesUniqueHash(schema.Properties)}";
        
        return schema;
    }
    
    public static string GetPropertiesUniqueHash(List<ProjectionDocumentPropertySchema> properties)
    {
        StringBuilder sb = new StringBuilder();

        foreach (var prop in properties)
        {
            sb.Append(prop.PropertyName);
            sb.Append(prop.PropertyType);

            foreach (var attributeProperty in prop.GetType().GetProperties())
            {
                if (attributeProperty.Name != "TypeId")
                {
                    sb.Append(attributeProperty.Name);
                    sb.Append(attributeProperty.GetValue(prop));
                }
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
        var hashBytes = md5.ComputeHash(bytes);

        return $"{Convert.ToHexString(hashBytes)}";
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