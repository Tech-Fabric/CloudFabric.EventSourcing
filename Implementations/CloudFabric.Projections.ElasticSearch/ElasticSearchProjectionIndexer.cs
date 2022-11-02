using Nest;

namespace CloudFabric.Projections.ElasticSearch;
public class ElasticSearchIndexer
{
    private readonly ElasticClient _client;

    public ElasticSearchIndexer(string uri,
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

    public async Task CreateOrUpdateIndex(string indexName, ProjectionDocumentSchema projectionDocumentSchema)
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

        var properties = GetPropertiesDescriptors(projectionDocumentSchema);

        var putMappingRequest = new PutMappingRequest(indexName)
        {
            Properties = properties
        };

        await _client.MapAsync(putMappingRequest);
    }

    private IProperties GetPropertiesDescriptors(ProjectionDocumentSchema projectionDocumentSchema)
    {
        var properties = new PropertiesDescriptor<object>();

        foreach (ProjectionDocumentPropertySchema prop in projectionDocumentSchema.Properties)
        {
            properties = GetPropertyDescriptor(properties, prop);
        }

        return ((IPromise<IProperties>)properties).Value;
    }

    private PropertiesDescriptor<object> GetPropertyDescriptor(PropertiesDescriptor<object> properties, ProjectionDocumentPropertySchema prop)
    {
        TypeCode propertyType = prop.PropertyType;

        // update property type to array element type in order to be indexed properly
        if (prop.IsNestedArray && prop.ArrayElementType.HasValue && prop.NestedObjectProperties?.Any() != true)
        {
            propertyType = prop.ArrayElementType.Value;
        }

        switch (propertyType)
        {
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Double:
            case TypeCode.Decimal:
                properties = properties.Number(p => p.Name(prop.PropertyName));
                break;
            case TypeCode.Boolean:
                properties = properties.Boolean(p => p.Name(prop.PropertyName));
                break;
            case TypeCode.String:
                if (prop.IsSearchable)
                {
                    var analyzer = string.IsNullOrEmpty(prop.Analyzer) ? "standard" : prop.Analyzer;
                    var searchAnalyzer = string.IsNullOrEmpty(prop.SearchAnalyzer) ? analyzer : prop.SearchAnalyzer;

                    properties = properties.Keyword(p =>
                    {
                        return p
                            .Name(prop.PropertyName)
                            .Fields(f => f
                                .Text(ss => ss
                                    .Name("folded")
                                    .Analyzer("folding-analyzer")
                                //.Boost(prop.SearchableBoost)
                                )
                                .Text(ss => ss
                                    .Name("text")
                                    .Analyzer(analyzer)
                                    .SearchAnalyzer(searchAnalyzer)
                                //.Boost(prop.SearchableBoost)
                                )
                            );
                        //.Boost(prop.SearchableBoost);
                    });
                }
                else
                {
                    properties = properties.Keyword(p => p.Name(prop.PropertyName));
                }
                break;
            case TypeCode.DateTime:
                properties = properties.Date(p => p.Name(prop.PropertyName));
                break;
            case TypeCode.Object:
                if (prop.IsNestedObject || prop.IsNestedArray)
                {
                    properties = properties.Nested<object>(p =>
                        p.Name(prop.PropertyName)
                            .Properties(x =>
                            {
                                var nestedProperties = new PropertiesDescriptor<object>();

                                foreach (var nestedProp in prop.NestedObjectProperties)
                                {
                                    nestedProperties = GetPropertyDescriptor(nestedProperties, nestedProp);
                                }

                                return nestedProperties;
                            })
                    );
                }
                else
                {
                    properties = properties.Text(p => p.Name(prop.PropertyName));
                }
                break;
            default:
                throw new Exception(
                    $"Elastic Search doesn't support {prop.PropertyType} type. PropertyName: {prop.PropertyName}"
                );
        }

        return properties;
    }
}
