using System.Text.Json;
using CloudFabric.Projections.Constants;
using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using Nest;

namespace CloudFabric.Projections.ElasticSearch;
public class ElasticSearchIndexer
{
    private readonly ElasticClient _client;
    private readonly ILogger<ElasticSearchIndexer> _logger;

    public ElasticSearchIndexer(ElasticSearchApiKeyAuthConnectionSettings apiKeyAuthConnectionSettings, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ElasticSearchIndexer>();
        var connectionSettings = new ConnectionSettings(apiKeyAuthConnectionSettings.CloudId, new ApiKeyAuthenticationCredentials(apiKeyAuthConnectionSettings.ApiKeyId,
                apiKeyAuthConnectionSettings.ApiKey))
            .ThrowExceptions()
            .DefaultFieldNameInferrer(x => x);
        _client = new ElasticClient(connectionSettings);
    }

    public ElasticSearchIndexer(ElasticSearchBasicAuthConnectionSettings basicAuthConnectionSettings,
        ILoggerFactory loggerFactory
    )
    {
        _logger = loggerFactory.CreateLogger<ElasticSearchIndexer>();
        
        var connectionSettings = new ConnectionSettings(new Uri(basicAuthConnectionSettings.Uri))
            .BasicAuthentication(basicAuthConnectionSettings.Username, basicAuthConnectionSettings.Password)
            .CertificateFingerprint(basicAuthConnectionSettings.CertificateThumbprint)
            .ThrowExceptions()
            // means that we do not change property names when indexing (like pascal case to camel case)
            .DefaultFieldNameInferrer(x => x);

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
            throw new ArgumentException($"Missing required parameter: {nameof(indexName)}");
        }

        if (projectionDocumentSchema.Properties.Count == 0)
        {
            throw new ArgumentException($"Index should not be empty, it requires at least one property");
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
                            .Custom("case-insensitive-analyzer", c => c
                                .Tokenizer("keyword")
                                .Filters("lowercase")
                            )
                            .Custom(SearchAnalyzers.UrlEmailAnalyzer, c => c
                                .Tokenizer("url-email-tokenizer")
                                .Filters("lowercase")
                            )
                        )
                        .Tokenizers(tokenizer => tokenizer
                            .UaxEmailUrl("url-email-tokenizer", u => u
                                .MaxTokenLength(255)
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

        var mappingPropertiesRequest = JsonSerializer.Serialize(properties.Values);
        _logger.LogInformation($"ES Indexer Request: {mappingPropertiesRequest}");
        
        var mappingResponse = await _client.MapAsync(putMappingRequest);
        if (!mappingResponse.IsValid)
        {
            _logger.LogError($"ES Indexer mapping error: {JsonSerializer.Serialize(mappingResponse.ServerError?.Error)}");
        }
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
                properties = properties.Number(p =>
                    p.Name(prop.PropertyName)
                        .Type(NumberType.Byte)
                );
                break;
            case TypeCode.Int16:
                properties = properties.Number(p =>
                    p.Name(prop.PropertyName)
                        .Type(NumberType.Short)
                );
                break;
            case TypeCode.Int32:
                properties = properties.Number(p =>
                    p.Name(prop.PropertyName)
                        .Type(NumberType.Integer)
                );
                break;
            case TypeCode.Int64:
                properties = properties.Number(p =>
                    p.Name(prop.PropertyName)
                        .Type(NumberType.Long)
                );
                break;
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                properties = properties.Number(p =>
                    p.Name(prop.PropertyName)
                        .Type(NumberType.UnsignedLong)
                );
                break;
            case TypeCode.Double:
                properties = properties.Number(p =>
                    p.Name(prop.PropertyName)
                        .Type(NumberType.Double)
                );
                break;
            case TypeCode.Decimal:
                properties = properties.Number(p => 
                    p.Name(prop.PropertyName)
                        .Type(NumberType.ScaledFloat)
                        .ScalingFactor(1000000)
                );
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
                                    .Name("text")
                                    .Analyzer(analyzer)
                                    .SearchAnalyzer(searchAnalyzer)
                                )
                                .Text(kw => kw
                                    .Name("case-insensitive")
                                    .Analyzer("case-insensitive-analyzer")
                                )
                            );
                    });
                }
                else if (prop.IsFilterable)
                {
                    properties = properties.Keyword(p => 
                        p.Name(prop.PropertyName)
                            .Fields(f => f
                                .Text(kw => kw
                                    .Name("case-insensitive")
                                    .Analyzer("case-insensitive-analyzer")
                                )
                            )
                    );
                }
                else
                {
                    properties = properties.Object<string>(p =>
                        p.Name(prop.PropertyName)
                            .Enabled(false)
                    );
                }
                break;
            case TypeCode.DateTime:
                properties = properties.Date(p => p.Name(prop.PropertyName));
                break;
            case TypeCode.Object:
                if (prop.IsNestedObject || prop.IsNestedArray)
                {
                    // https://www.elastic.co/guide/en/elasticsearch/reference/current/array.html
                    // In Elasticsearch, there is no dedicated array data type.
                    // Any field can contain zero or more values by default, however, all values in the array must be of the same data type.

                    if (prop.NestedObjectProperties.Count > 0)
                    {
                        properties = properties.Nested<object>(
                            p =>
                                p.Name(prop.PropertyName)
                                    .Properties(
                                        x =>
                                        {
                                            var nestedProperties = new PropertiesDescriptor<object>();

                                            foreach (var nestedProp in prop.NestedObjectProperties)
                                            {
                                                nestedProperties = GetPropertyDescriptor(nestedProperties, nestedProp);
                                            }

                                            return nestedProperties;
                                        }
                                    )
                        );
                    }
                    else
                    {
                        properties = properties.Text(p => p.Name(prop.PropertyName));
                    }
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
