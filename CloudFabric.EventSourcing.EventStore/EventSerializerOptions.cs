using System.Text.Json;

namespace CloudFabric.EventSourcing.EventStore;

public static class EventSerializerOptions
{
    public static JsonSerializerOptions Options
    {
        get
        {
            return new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        }
    }
}
