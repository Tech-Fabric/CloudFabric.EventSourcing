using System.Text.Json;
using CloudFabric.EventSourcing.EventStore.Persistence;

namespace CloudFabric.EventSourcing.EventStore.InMemory;

public class EventAddedEventArgs : EventArgs
{
    public IEvent Event { get; set; }

    public string PartitionKey { get; set; }
}

public class InMemoryEventStore : IEventStore
{
    private readonly Dictionary<(string StreamId, string PartitionKey), List<string>> _eventsContainer;

    public InMemoryEventStore(
        Dictionary<(string StreamId, string PartitionKey), List<string>> eventsContainer
    )

    {
        _eventsContainer = eventsContainer;
    }

    public Task Initialize()
    {
        return Task.CompletedTask;
    }

    public Task DeleteAll()
    {
        _eventsContainer.Clear();
        return Task.CompletedTask;
    }

    public async Task<EventStream> LoadStreamAsyncOrThrowNotFound(string streamId, string partitionKey)
    {
        var eventWrappers = await LoadOrderedEventWrappers(streamId, partitionKey);
        if (eventWrappers.Count == 0)
        {
            throw new NotFoundException();
        }

        int version = eventWrappers.Max(x => x.StreamInfo.Version);
        var events = new List<IEvent>();
        foreach (var wrapper in eventWrappers)
        {
            events.Add(wrapper.GetEvent());
        }

        return new EventStream(streamId, version, events);
    }

    public async Task<EventStream> LoadStreamAsync(string streamId, string partitionKey)
    {
        var eventWrappers = await LoadOrderedEventWrappers(streamId, partitionKey);

        int version = eventWrappers.Count > 0
            ? eventWrappers.Max(x => x.StreamInfo.Version)
            : 0;
        var events = new List<IEvent>();
        foreach (var wrapper in eventWrappers)
        {
            events.Add(wrapper.GetEvent());
        }

        return new EventStream(streamId, version, events);
    }

    public async Task<EventStream> LoadStreamAsync(string streamId, string partitionKey, int fromVersion)
    {
        var eventWrappers = await LoadOrderedEventWrappersFromVersion(streamId, partitionKey, fromVersion);

        if (eventWrappers.Count == 0)
        {
            throw new NotFoundException();
        }

        int version = eventWrappers.Max(x => x.StreamInfo.Version);
        var events = new List<IEvent>();
        foreach (var wrapper in eventWrappers)
        {
            events.Add(wrapper.GetEvent());
        }

        return new EventStream(streamId, version, events);
    }

    public async Task<List<IEvent>> LoadEventsAsync(string partitionKey, DateTime? dateFrom)
    {
        if (_eventsContainer == null || !_eventsContainer.Any())
        {
            return new List<IEvent>();
        }

        var events = _eventsContainer
            .Where(x => x.Key.PartitionKey == partitionKey)
            .SelectMany(x => x.Value)
            .Select(x => JsonSerializer.Deserialize<EventWrapper>(x).GetEvent())
            .Where(x => !dateFrom.HasValue || x.Timestamp >= dateFrom)
            .OrderBy(x => x.Timestamp)
            .ToList();

        return events;
    }

    public async Task<bool> AppendToStreamAsync(
        EventUserInfo eventUserInfo,
        string streamId,
        string partitionKey,
        int expectedVersion,
        IEnumerable<IEvent> events
    )
    {
        var lockObject = new object();
        lock (lockObject)
        {
            // Load stream and verify version hasn't been changed yet.
            var eventStream = LoadStreamAsync(streamId, partitionKey).GetAwaiter().GetResult();

            if (eventStream.Version != expectedVersion)
            {
                return false;
            }

            var wrappers = PrepareEvents(eventUserInfo, streamId, partitionKey, expectedVersion, events);
            var stream = _eventsContainer.ContainsKey((streamId, partitionKey))
                ? _eventsContainer[(streamId, partitionKey)]
                : new List<string>();

            foreach (var wrapper in wrappers)
            {
                stream.Add(JsonSerializer.Serialize(wrapper));

                EventHandler<EventAddedEventArgs> handler = EventAdded;
                if (handler != null)
                {
                    handler(this, new EventAddedEventArgs() { Event = wrapper.GetEvent(), PartitionKey = partitionKey });
                }
            }

            if (!_eventsContainer.ContainsKey((streamId, partitionKey)))
            {
                _eventsContainer.Add((streamId, partitionKey), stream);
            }
            else
            {
                _eventsContainer[(streamId, partitionKey)] = stream;
            }
        }

        return true;
    }

    public event EventHandler<EventAddedEventArgs> EventAdded;

    private async Task<List<EventWrapper>> LoadOrderedEventWrappers(string streamId, string partitionKey)
    {
        List<string> eventData = _eventsContainer.ContainsKey((streamId, partitionKey))
            ? _eventsContainer[(streamId, partitionKey)]
            : new List<string>();

        var eventWrappers = new List<EventWrapper>();

        foreach (var data in eventData)
        {
            var eventWrapper = JsonSerializer.Deserialize<EventWrapper>(data);
            eventWrappers.Add(eventWrapper);
        }

        eventWrappers = eventWrappers.OrderBy(x => x.StreamInfo.Version).ToList();
        return eventWrappers;
    }

    private async Task<List<EventWrapper>> LoadOrderedEventWrappersFromVersion(string streamId, string partitionKey, int version)
    {
        List<string> eventData =
            _eventsContainer.ContainsKey((streamId, partitionKey))
                ? _eventsContainer[(streamId, partitionKey)]
                : new List<string>();
        var eventWrappers = new List<EventWrapper>();

        foreach (var data in eventData)
        {
            var eventWrapper = JsonSerializer.Deserialize<EventWrapper>(data);
            if (eventWrapper.StreamInfo.Version >= version)
            {
                eventWrappers.Add(eventWrapper);
            }
        }

        eventWrappers = eventWrappers.OrderBy(x => x.StreamInfo.Version).ToList();
        return eventWrappers;
    }

    private static List<EventWrapper> PrepareEvents(
        EventUserInfo eventUserInfo, string streamId, string partitionKey, int expectedVersion, IEnumerable<IEvent> events
    )
    {
        if (string.IsNullOrEmpty(eventUserInfo.UserId))
            throw new Exception("UserInfo.Id must be set to a value.");

        var items = events.Select(
            e => new EventWrapper
            {
                // Id = $"{streamId}:{++expectedVersion}:{e.GetType().Name}",
                Id = $"{streamId}:{++expectedVersion}", //:{e.GetType().Name}",
                StreamInfo = new StreamInfo { Id = streamId, Version = expectedVersion, PartitionKey = partitionKey },
                EventType = e.GetType().AssemblyQualifiedName,
                EventData = JsonSerializer.SerializeToElement(e, e.GetType()),
                UserInfo = JsonSerializer.SerializeToElement(eventUserInfo, eventUserInfo.GetType())
            }
        );

        return items.ToList();
    }

    #region Snapshot Functionality

    // private async Task<TSnapshot> LoadSnapshotAsync<TSnapshot>(string streamId)
    // {
    //     //Container container = _client.GetContainer(_databaseId, _containerId);
    //
    //     PartitionKey partitionKey = new PartitionKey(streamId);
    //
    //     var response = await container.ReadItemAsync<TSnapshot>(streamId, partitionKey);
    //     if (response.StatusCode == HttpStatusCode.OK)
    //     {
    //         return response.Resource;
    //     }
    //
    //     return default(TSnapshot);
    // }

    #endregion
}