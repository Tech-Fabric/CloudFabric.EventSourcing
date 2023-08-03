using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudFabric.EventSourcing.EventStore.Persistence;

public class ItemWrapper
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; }

    [JsonPropertyName("item_data")]
    public string ItemData { get; set; }
}
