using System.Reflection;
using Nest;

namespace CloudFabric.Projections.ElasticSearch;

public class ElasticSearchProjectionIndexer
{
    private readonly ElasticClient _client;

    public ElasticSearchProjectionIndexer(string uri,
        string username,
        string password,
        string certificateFingerprint
    )
    {
        var connectionSettings = new ConnectionSettings(new Uri(uri));
        connectionSettings.BasicAuthentication(username, password);
        connectionSettings.CertificateFingerprint(certificateFingerprint);
        connectionSettings.ThrowExceptions();

        // means that we do not change property names when indexing (like pascal case to camel case)
        connectionSettings.DefaultFieldNameInferrer(x => x);

        _client = new ElasticClient(connectionSettings);
    }

    public async Task DeleteIndex(string indexName)
    {
        await _client.Indices.DeleteAsync(indexName);
    }

    public async Task CreateIndex(string indexName, ProjectionDocumentSchema projectionDocumentSchema)
    {
        if (string.IsNullOrWhiteSpace(indexName))
        {
            throw new Exception($"Missing required parameter: {nameof(indexName)}");
        }

        var response = await _client.Indices.ExistsAsync(
            new IndexExistsRequest(indexName)
        );

        if (!response.Exists)
        {
            var descriptor = new CreateIndexDescriptor(indexName)
                .Settings(s => s
                    .Analysis(analysis => analysis
                        .Analyzers(analyzers => analyzers
                            .Custom("folding-analyzer", c => c
                                .Tokenizer("standard")
                                .Filters("lowercase", "asciifolding")
                            )
                            .Custom("keyword-custom", c => c
                                .Tokenizer("keyword")
                                .Filters("lowercase")
                            )   
                        )
                    )
                );

            var createIndexResponse = await _client.Indices.CreateAsync(descriptor);
            if (!createIndexResponse.IsValid)
            {
                throw new Exception($"ES index creation failed: {createIndexResponse.ServerError}");
            }
        }

        //var properties = GetPropertiesDescriptors(projectionDocumentSchema);

        //var putMappingRequest = new PutMappingRequest(indexName)
        //{
        //    Properties = properties
        //};

        //await _client.MapAsync(putMappingRequest);
    }

    //private IProperties GetPropertiesDescriptors(ProjectionDocumentSchema projectionDocumentSchema)
    //{
    //    var properties = new PropertiesDescriptor<>();

    //    PropertyInfo[] props = typeof(T).GetProperties();
    //    foreach (PropertyInfo prop in props)
    //    {
    //        object[] attrs = prop.GetCustomAttributes(true);
    //        foreach (object attr in attrs)
    //        {
    //            SearchablePropertyAttribute propertyAttribute = attr as SearchablePropertyAttribute;
    //            if (propertyAttribute == null)
    //            {
    //                continue;
    //            }

    //            Type propertyType = prop.PropertyType;

    //            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
    //            {
    //                propertyType = Nullable.GetUnderlyingType(propertyType);
    //            }
    //            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
    //            {
    //                propertyType = propertyType.GetMethod("get_Item").ReturnType;
    //            }

    //            switch (Type.GetTypeCode(propertyType))
    //            {
    //                case TypeCode.Int32:
    //                case TypeCode.Int64:
    //                case TypeCode.Double:
    //                    properties = properties.Number(p =>
    //                    {
    //                        return p.Name(prop.Name);
    //                    });
    //                    break;
    //                case TypeCode.Boolean:
    //                    properties = properties.Boolean(p =>
    //                    {
    //                        return p.Name(prop.Name);
    //                    });
    //                    break;
    //                case TypeCode.String:
    //                    if (propertyAttribute.IsSearchable)
    //                    {
    //                        var analyzer = string.IsNullOrEmpty(propertyAttribute.Analyzer) ? "standard" : propertyAttribute.Analyzer;
    //                        var searchAnalyzer = string.IsNullOrEmpty(propertyAttribute.SearchAnalyzer) ? analyzer : propertyAttribute.SearchAnalyzer;

    //                        properties = properties.Keyword(p =>
    //                        {
    //                            return p
    //                                .Name(prop.Name)
    //                                .Fields(f => f
    //                                    .Text(ss => ss
    //                                        .Name("folded")
    //                                        .Analyzer("folding-analyzer")
    //                                        .Boost(propertyAttribute.SearchableBoost)
    //                                    )
    //                                    .Text(ss => ss
    //                                        .Name("text")
    //                                        .Analyzer(analyzer)
    //                                        .SearchAnalyzer(searchAnalyzer)
    //                                        .Boost(propertyAttribute.SearchableBoost)
    //                                    )
    //                                )
    //                                .Boost(propertyAttribute.SearchableBoost);
    //                        });
    //                    }
    //                    else
    //                    {
    //                        properties = properties.Keyword(p => p.Name(prop.Name));
    //                    }
    //                    break;
    //                case TypeCode.DateTime:
    //                    properties = properties.Date(p =>
    //                    {
    //                        return p.Name(prop.Name);
    //                    });
    //                    break;
    //                case TypeCode.Object:
    //                    //if (propertyAttribute.IsNested)
    //                    //{
    //                    properties = properties.Nested<object>(p => p.Name(prop.Name));
    //                    //}
    //                    break;
    //                default:
    //                    throw new Exception(
    //                        $"Elastic Search doesn't support {prop.PropertyType.Name} type. TypeCode: {Type.GetTypeCode(prop.PropertyType)}"
    //                    );
    //            }
    //        }
    //    }

    //    return ((IPromise<IProperties>)properties).Value;
    //}
}
