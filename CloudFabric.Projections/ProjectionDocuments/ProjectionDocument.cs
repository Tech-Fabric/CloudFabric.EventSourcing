using System.Text.Json.Serialization;
using CloudFabric.Projections.Attributes;

namespace CloudFabric.Projections;

public class ProjectionDocument
{
    [JsonPropertyName("id")]
    [ProjectionDocumentProperty(IsKey = true)]
    public Guid? Id { get; set; }

    [JsonPropertyName("partitionKey")]
    [ProjectionDocumentProperty(IsFilterable = true)]
    public string? PartitionKey { get; set; }
    
    [JsonPropertyName("updatedAt")]
    [ProjectionDocumentProperty(IsFilterable = true)]
    public DateTime UpdatedAt { get; set; }
}