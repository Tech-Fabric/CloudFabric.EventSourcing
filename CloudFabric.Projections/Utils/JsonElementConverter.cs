using System.Text.Json;

namespace CloudFabric.Projections.Utils;
public static class JsonToObjectConverter
{
    public static object? Convert(string json)
    {
        var jsonElement = JsonDocument.Parse(json).RootElement;
        return Convert(jsonElement);
    }

    public static object? Convert(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Array)
        {
            var array = json.Deserialize<List<JsonElement>>();
            var resultArray = new List<object?>();

            foreach (var arrayItem in array)
            {
                if (arrayItem.ValueKind == JsonValueKind.Array || arrayItem.ValueKind == JsonValueKind.Object)
                {
                    resultArray.Add(Convert(arrayItem));
                }
                else
                {
                    resultArray.Add(DeserializePrimitive(arrayItem));
                }
            }

            return resultArray;
        }
        else if (json.ValueKind == JsonValueKind.Object)
        {
            var dictObject = json.Deserialize<Dictionary<string, JsonElement>>();
            var resultObject = new Dictionary<string, object?>();

            foreach (var property in dictObject)
            {
                resultObject[property.Key] = Convert(property.Value);
            }

            return resultObject;
        }
        else
        {
            return DeserializePrimitive(json);
        }
    }

    private static object? DeserializePrimitive(JsonElement json)
    {
        return json.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.String => json.GetString(),
            JsonValueKind.True => json.GetBoolean(),
            JsonValueKind.False => json.GetBoolean(),
            JsonValueKind.Number => json.GetDecimal(),
            _ => throw new Exception($"Invalid JsonValueKind {json.ValueKind}")
        };
    }
}
