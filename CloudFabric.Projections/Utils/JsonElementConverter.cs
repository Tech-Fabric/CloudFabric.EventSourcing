using System.Text.Json;

namespace CloudFabric.Projections.Utils;
public static class JsonToObjectConverter
{
    public static object? Convert(string json, ProjectionDocumentPropertySchema propertySchema)
    {
        var jsonElement = JsonDocument.Parse(json).RootElement;
        return Convert(jsonElement, propertySchema);
    }

    public static object? Convert(JsonElement json, ProjectionDocumentPropertySchema propertySchema)
    {
        if (propertySchema.IsNestedArray)
        {
            var array = json.Deserialize<List<JsonElement>>();
            var resultArray = new List<object?>();

            foreach (var arrayItem in array)
            {
                if (propertySchema.ArrayElementType == TypeCode.Object)
                {
                    if (propertySchema.NestedObjectProperties.Count == 0)
                    {
                        throw new Exception(
                            $"Invalid nested object configuration for projection property {propertySchema.PropertyName}." +
                            $"It appears that the property marked with IsNestedArray has array items class " +
                            $"not decorated with [ProjectionDocumentProperty] attribute."
                        );
                    }
                    
                    var dictObject = arrayItem.Deserialize<Dictionary<string, JsonElement>>();
                    var resultObject = new Dictionary<string, object?>();

                    foreach (var property in dictObject)
                    {
                        resultObject[property.Key] = Convert(
                            property.Value, 
                            propertySchema.NestedObjectProperties.First(x => x.PropertyName == property.Key)
                        );
                    }

                    resultArray.Add(resultObject);
                }
                else
                {
                    resultArray.Add(DeserializePrimitive(arrayItem, propertySchema.ArrayElementType.Value));
                }
            }

            return resultArray;
        }
        else if (propertySchema.IsNestedObject)
        {
            var dictObject = json.Deserialize<Dictionary<string, JsonElement>>();
            var resultObject = new Dictionary<string, object?>();

            foreach (var property in dictObject)
            {
                resultObject[property.Key] = Convert(
                    property.Value,
                    propertySchema.NestedObjectProperties.First(x => x.PropertyName == property.Key)
                );
            }

            return resultObject;
        }
        else
        {
            return DeserializePrimitive(json, propertySchema.PropertyType);
        }
    }

    private static object? DeserializePrimitive(JsonElement json, TypeCode propertyType)
    {
        return propertyType switch
        {
            TypeCode.Boolean => json.GetBoolean(),
            TypeCode.SByte => json.GetSByte(),
            TypeCode.Byte => json.GetByte(),
            TypeCode.Int16 => json.GetInt16(),
            TypeCode.UInt16 => json.GetUInt16(),
            TypeCode.Int32 => json.GetInt32(),
            TypeCode.UInt32 => json.GetUInt32(),
            TypeCode.Int64 => json.GetInt64(),
            TypeCode.UInt64 => json.GetUInt64(),
            TypeCode.Single => json.GetSingle(),
            TypeCode.Double => json.GetDouble(),
            TypeCode.Decimal => json.GetDecimal(),
            TypeCode.DateTime => json.GetDateTime(),
            TypeCode.String => json.GetString(),
            TypeCode.Object => json.GetGuid(),
            TypeCode.DBNull => null,
            TypeCode.Empty => null,
            _ => throw new Exception($"Failed to deserialize json element for object {json}")
        };
    }
}
