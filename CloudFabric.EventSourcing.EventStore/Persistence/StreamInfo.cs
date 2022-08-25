using System.Text.Json.Serialization;

namespace CloudFabric.EventSourcing.EventStore.Persistence;

public record StreamInfo
{
    [JsonPropertyName("id")] 
    public string Id { get; set; }

    [JsonPropertyName("version")] 
    public int Version { get; set; }
}