using System.Text.Json.Serialization;

namespace CloudFabric.EventSourcing.EventStore.Persistence;

public record EventUserInfo
{
    public EventUserInfo()
    {
        UserId = "unauthorized";
    }

    public EventUserInfo(string userId)
    {
        UserId = userId;
    }

    [JsonPropertyName("userId")]
    public string UserId { get; }
}