using System.Text.Json.Serialization;
using CloudFabric.Projections.Attributes;

namespace CloudFabric.Projections;

public class ProjectionDocument
{
    [JsonPropertyName("id")]
    [ProjectionDocumentProperty(IsSearchable = true, IsKey = true)]
    public string? Id { get; set; }

    [JsonPropertyName("partitionKey")]
    [ProjectionDocumentProperty(IsSearchable = true, IsFilterable = true)]
    public string? PartitionKey { get; set; }
}