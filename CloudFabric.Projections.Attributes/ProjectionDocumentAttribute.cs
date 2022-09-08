using System.Reflection;
using System.Text;

namespace CloudFabric.Projections.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ProjectionDocumentAttribute : Attribute
{
    public static string GetIndexName<TProjectionDocument>()
    {
        var properties = GetQueryableProperties<TProjectionDocument>();

        StringBuilder sb = new StringBuilder();

        foreach (var prop in properties.Keys)
        {
            sb.Append(prop.Name);
            sb.Append(prop.PropertyType.Name);

            foreach (var attributeProperty in properties[prop].GetType().GetProperties())
            {
                if (attributeProperty.Name != "TypeId")
                {
                    sb.Append(attributeProperty.Name);
                    sb.Append(attributeProperty.GetValue(properties[prop]));
                }
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
        var hashBytes = md5.ComputeHash(bytes);

        return $"{typeof(TProjectionDocument).Name}_{Convert.ToHexString(hashBytes)}";
    }

    public static string? GetKeyPropertyName<T>()
    {
        PropertyInfo[] props = typeof(T).GetProperties();
        foreach (PropertyInfo prop in props)
        {
            object[] attrs = prop.GetCustomAttributes(true);
            foreach (object attr in attrs)
            {
                if (attr is ProjectionDocumentPropertyAttribute { IsKey: true })
                {
                    return prop.Name;
                }
            }
        }

        return null;
    }

    public static Dictionary<PropertyInfo, ProjectionDocumentPropertyAttribute> GetProperties<T>()
    {
        Dictionary<PropertyInfo, ProjectionDocumentPropertyAttribute> properties =
            new Dictionary<PropertyInfo, ProjectionDocumentPropertyAttribute>();

        PropertyInfo[] props = typeof(T).GetProperties();
        foreach (PropertyInfo prop in props)
        {
            object[] attrs = prop.GetCustomAttributes(true);
            foreach (object attr in attrs)
            {
                if (attr is ProjectionDocumentPropertyAttribute propertyAttribute)
                {
                    properties.Add(prop, propertyAttribute);
                }
            }
        }

        return properties;
    }

    public static Dictionary<PropertyInfo, ProjectionDocumentPropertyAttribute> GetFacetableProperties<T>()
    {
        Dictionary<PropertyInfo, ProjectionDocumentPropertyAttribute> facetableProperties =
            new Dictionary<PropertyInfo, ProjectionDocumentPropertyAttribute>();

        PropertyInfo[] props = typeof(T).GetProperties();
        foreach (PropertyInfo prop in props)
        {
            object[] attrs = prop.GetCustomAttributes(true);
            foreach (object attr in attrs)
            {
                if (attr is ProjectionDocumentPropertyAttribute { IsFacetable: true } propertyAttribute)
                {
                    facetableProperties.Add(prop, propertyAttribute);
                }
            }
        }

        return facetableProperties;
    }

    public static List<string> GetFacetablePropertyNames<T>()
    {
        return GetFacetableProperties<T>().Keys.Select(p => p.Name).ToList();
    }

    public static Dictionary<PropertyInfo, ProjectionDocumentPropertyAttribute> GetQueryableProperties<T>()
    {
        Dictionary<PropertyInfo, ProjectionDocumentPropertyAttribute> searchableProperties =
            new Dictionary<PropertyInfo, ProjectionDocumentPropertyAttribute>();

        PropertyInfo[] props = typeof(T).GetProperties();
        foreach (PropertyInfo prop in props)
        {
            object[] attrs = prop.GetCustomAttributes(true);
            foreach (object attr in attrs)
            {
                if (attr is ProjectionDocumentPropertyAttribute { IsSearchable: true } propertyAttribute)
                {
                    searchableProperties.Add(prop, propertyAttribute);
                }
            }
        }

        return searchableProperties;
    }

    public static List<string> GetQueryablePropertyNames<T>()
    {
        return GetQueryableProperties<T>().Keys.Select(p => p.Name).ToList();
    }

    public static TypeCode? GetPropertyPathTypeCode<T>(string pathName)
    {
        return GetPropertyPathTypeCode(pathName, typeof(T));
    }

    public static TypeCode? GetPropertyPathTypeCode(string pathName, Type type)
    {
        var pathParts = pathName.Split('.');
        if (pathParts.Count() <= 1)
        {
            return GetPropertyTypeCode(pathName, type);
        }

        MemberInfo[] members = type.GetMember(pathParts[0]);

        if (members.Length == 0)
        {
            throw new Exception($"Failed to get member {pathParts[0]} from type {type.Name}");
        }

        var p = members[0];

        Type? propertyType = p.DeclaringType;

        if (p.MemberType == MemberTypes.Method || p.MemberType == MemberTypes.Constructor ||
            p.MemberType == MemberTypes.Event)
        {
            throw new Exception(
                $"It's not possible to search by ${pathParts[0]} " +
                $"member of type ${type.Name} since it's not field or property");
        }
        else if (p.MemberType == MemberTypes.Property)
        {
            propertyType = (p as PropertyInfo)?.PropertyType;
        }
        else if (p.MemberType == MemberTypes.Field)
        {
            propertyType = (p as FieldInfo)?.FieldType;
        }

        if (propertyType?.GetTypeInfo().IsGenericType == true &&
            propertyType.GetGenericTypeDefinition() == typeof(List<>))
        {
            propertyType = propertyType.GetGenericArguments()[0];
        }

        if (propertyType?.GetType().IsGenericType == true &&
            propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            propertyType = Nullable.GetUnderlyingType(propertyType);
        }

        return propertyType == null ? null : GetPropertyPathTypeCode(string.Join(".", pathParts.Skip(1)), propertyType);
    }

    public static TypeCode? GetPropertyTypeCode(string propertyName, Type type)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            throw new Exception($"GetPropertyTypeCode: propertyName can't be empty, type: {type.FullName}");
        }

        PropertyInfo? propertyInfo = type.GetProperty(propertyName);

        if (propertyInfo == null)
        {
            throw new Exception($"GetPropertyTypeCode: can't find property {propertyName} on type {type.FullName}");
        }

        return GetPropertyTypeCode(propertyInfo, type);
    }

    public static TypeCode? GetPropertyTypeCode<T>(string propertyName)
    {
        return GetPropertyPathTypeCode(propertyName, typeof(T));
    }

    public static TypeCode GetPropertyTypeCode(PropertyInfo propertyInfo, Type type)
    {
        if (propertyInfo.PropertyType.IsGenericType)
        {
            if (propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return Type.GetTypeCode(propertyInfo.PropertyType.GetGenericArguments()[0]);
            }
        }

        return Type.GetTypeCode(propertyInfo.PropertyType);
    }
}