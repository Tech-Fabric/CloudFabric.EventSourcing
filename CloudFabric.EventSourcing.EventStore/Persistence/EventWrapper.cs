using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudFabric.EventSourcing.EventStore.Persistence;

public record EventWrapper
{
    [JsonPropertyName("id")] 
    public Guid? Id { get; set; }

    [JsonPropertyName("stream")] 
    public StreamInfo? StreamInfo { get; set; }

    [JsonPropertyName("eventType")] 
    public string? EventType { get; set; }

    [JsonPropertyName("eventData")] 
    public JsonElement EventData { get; set; }

    [JsonPropertyName("userInfo")] 
    public JsonElement UserInfo { get; set; }

    public IEvent GetEvent()
    {
        if (string.IsNullOrEmpty(EventType))
        {
            throw new InvalidOperationException("Can't get event data for event which EventType is null");
        }

        try
        {
            var eventType = Type.GetType(EventType, EventTypeAssemblyResolver, null);

            if (eventType == null)
            {
                throw new Exception("Couldn't find event type. Make sure it's in correct namespace.");
            }

            var e = (IEvent?)EventData.Deserialize(eventType, EventStoreSerializerOptions.Options);

            if (e == null)
            {
                throw new Exception("Event deserialization returned null.");
            }

            return e;
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to convert to an Event. Make sure the event " +
                                "class is in the correct namespace.", ex);
        }
    }
    
    private static System.Reflection.Assembly EventTypeAssemblyResolver(System.Reflection.AssemblyName assemblyName)
    {
        assemblyName.Version = null;
        return System.Reflection.Assembly.Load(assemblyName);
    }
}