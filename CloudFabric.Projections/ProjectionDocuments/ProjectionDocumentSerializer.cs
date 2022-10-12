namespace CloudFabric.Projections;

public static class ProjectionDocumentSerializer
{
    public static Dictionary<string, object?> SerializeToDictionary<TDocument>(TDocument document)
        where TDocument : ProjectionDocument
    {
        var documentDictionary = new Dictionary<string, object?>();

        var propertyInfos = typeof(TDocument).GetProperties();
        foreach (var propertyInfo in propertyInfos)
        {
            object? value = propertyInfo.GetValue(document);

            // check value type
            if (value != null && (
                    propertyInfo.PropertyType.IsEnum ||
                    (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && propertyInfo.PropertyType.GetGenericArguments()[0].IsEnum)
                )
            )
            {
                value = (int)value;
            }

            documentDictionary[propertyInfo.Name] = value;
        }

        return documentDictionary;
    }

    public static TDocument DeserializeFromDictionary<TDocument>(Dictionary<string, object?> document)
        where TDocument: ProjectionDocument
    {
        var documentTypedInstance = Activator.CreateInstance<TDocument>();

        foreach (var propertyName in document.Keys)
        {
            var propertyInfo = typeof(TDocument).GetProperty(propertyName);

            if (propertyInfo == null)
            {
                continue;
            }

            // check types
            object? value = document[propertyName];
            if (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && propertyInfo.PropertyType.GetGenericArguments()[0].IsEnum)
            {
                value = Enum.TryParse(propertyInfo.PropertyType.GetGenericArguments()[0], value?.ToString(), out var parsedValue)
                    ? parsedValue
                    : null;
            }
            else if (propertyInfo.PropertyType.IsEnum)
            {
                value = Convert.ToInt32(value);
            }
            else if (propertyInfo.PropertyType == typeof(Guid?) || propertyInfo.PropertyType == typeof(Guid))
            {
                value = Guid.TryParse(value?.ToString(), out var parsedValue)
                    ? parsedValue
                    : null;
            }

            propertyInfo?.SetValue(documentTypedInstance, value);
        }

        return documentTypedInstance;
    }
}
