using System.Collections;

namespace CloudFabric.Projections;

public static class ProjectionDocumentSerializer
{
    public static Dictionary<string, object?> SerializeToDictionary<TDocument>(TDocument document)
    {
        var documentDictionary = new Dictionary<string, object?>();

        var propertyInfos = typeof(TDocument).GetProperties();
        foreach (var propertyInfo in propertyInfos)
        {
            object? value = propertyInfo.GetValue(document);

            // check value type
            if (value != null)
            {
                if (propertyInfo.PropertyType.IsEnum ||
                    (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && propertyInfo.PropertyType.GetGenericArguments()[0].IsEnum)
                )
                {
                    value = (int)value;
                }
                else if (propertyInfo.PropertyType.IsClass)
                {
                    if (typeof(IList).IsAssignableFrom(propertyInfo.PropertyType))
                    {
                        List<object?> list = new();

                        foreach (var listElem in (value as IList))
                        {
                            var genericListType = propertyInfo.PropertyType.GenericTypeArguments[0];
                            if (!genericListType.IsClass || genericListType == typeof(string))
                            {
                                list.Add(listElem);
                            }
                            else
                            {
                                var arrayElement = typeof(ProjectionDocumentSerializer)
                                    .GetMethod(nameof(SerializeToDictionary))
                                    .MakeGenericMethod(genericListType)
                                    .Invoke(null, new object[] { listElem });

                                list.Add(arrayElement);
                            }
                        }

                        value = list;
                    }
                    else if (propertyInfo.PropertyType != typeof(string))
                    {
                        value = typeof(ProjectionDocumentSerializer)
                            .GetMethod(nameof(SerializeToDictionary))
                            .MakeGenericMethod(propertyInfo.PropertyType)
                            .Invoke(null, new object[] { value });
                    }
                }
            }

            documentDictionary[propertyInfo.Name] = value;
        }

        return documentDictionary;
    }

    public static TDocument DeserializeFromDictionary<TDocument>(Dictionary<string, object?> document)
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
            else if (propertyInfo.PropertyType == typeof(DateTime?) || propertyInfo.PropertyType == typeof(DateTime))
            {
                value = DateTime.TryParse(value?.ToString(), out var parsedValue)
                    ? parsedValue
                    : null;
            }
            else if (propertyInfo.PropertyType == typeof(decimal?) || propertyInfo.PropertyType == typeof(decimal))
            {
                value = decimal.TryParse(value?.ToString(), out var parsedValue)
                    ? parsedValue
                    : null;
            }
            else if (propertyInfo.PropertyType.IsClass)
            {
                // check if list - works only with List<> for now
                if (typeof(IList).IsAssignableFrom(propertyInfo.PropertyType))
                {
                    Type listType = typeof(List<>).MakeGenericType(propertyInfo.PropertyType.GenericTypeArguments[0]);
                    IList list = (IList)Activator.CreateInstance(listType);

                    foreach (var listElem in (value as IList))
                    {
                        var genericListType = propertyInfo.PropertyType.GenericTypeArguments[0];
                        if (!genericListType.IsClass || genericListType == typeof(string))
                        {
                            list.Add(Convert.ChangeType(listElem, genericListType));
                        }
                        else
                        {
                            var arrayElement = typeof(ProjectionDocumentSerializer)
                                .GetMethod(nameof(DeserializeFromDictionary))
                                .MakeGenericMethod(genericListType)
                                .Invoke(null, new object[] { (Dictionary<string, object?>)listElem });

                            list.Add(arrayElement);
                        }
                    }

                    value = list;
                }
                // check if object
                else if (propertyInfo.PropertyType != typeof(string))
                {
                    value = typeof(ProjectionDocumentSerializer)
                        .GetMethod(nameof(DeserializeFromDictionary))
                        .MakeGenericMethod(propertyInfo.PropertyType)
                        .Invoke(null, new object[] { (Dictionary<string, object?>)value });
                }
            }

            propertyInfo?.SetValue(documentTypedInstance, value);
        }

        return documentTypedInstance;
    }
}
