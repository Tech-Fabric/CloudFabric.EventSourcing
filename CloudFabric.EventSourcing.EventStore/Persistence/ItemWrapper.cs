using System.Text.Json.Serialization;

namespace CloudFabric.EventSourcing.EventStore.Persistence;

public class ItemWrapper
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("partition_key")]
    public string? PartitionKey { get; set; }

    [JsonPropertyName("data")]
    public string? ItemData { get; set; }
}
