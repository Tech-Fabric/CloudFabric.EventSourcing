namespace CloudFabric.Projections.ElasticSearch;

public record ElasticSearchApiKeyAuthConnectionSettings(string ApiKeyId, string ApiKey, string CloudId)
{
    public readonly string ApiKeyId = ApiKeyId;
    public readonly string ApiKey = ApiKey;
    public readonly string CloudId = CloudId;
}
