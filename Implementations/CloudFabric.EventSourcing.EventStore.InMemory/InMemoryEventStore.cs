using System.Text.Json;
using CloudFabric.EventSourcing.EventStore.Persistence;

namespace CloudFabric.EventSourcing.EventStore.InMemory;

public class EventAddedEventArgs : EventArgs
{
    public IEvent Event { get; set; }
}

public class InMemoryEventStore : IEventStore
{
    private readonly Dictionary<(Guid StreamId, string PartitionKey), List<string>> _eventsContainer;
    private readonly List<Func<IEvent, Task>> _eventAddedEventHandlers = new();

    public InMemoryEventStore(
        Dictionary<(Guid StreamId, string PartitionKey), List<string>> eventsContainer
    )
    {
        _eventsContainer = eventsContainer;
    }

    public Task Initialize(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void SubscribeToEventAdded(Func<IEvent, Task> handler)
    {
        _eventAddedEventHandlers.Add(handler);
    }

    public void UnsubscribeFromEventAdded(Func<IEvent, Task> handler)
    {
        _eventAddedEventHandlers.Remove(handler);
    }

    public Task DeleteAll(CancellationToken cancellationToken = default)
    {
        _eventsContainer.Clear();
        return Task.CompletedTask;
    }

    public async Task<bool> HardDeleteAsync(Guid streamId, string partitionKey, CancellationToken cancellationToken = default)
    {
        var lockObject = new object();

        lock (lockObject)
        {
            return _eventsContainer.Remove((streamId, partitionKey));
        }       
    }

    public async Task<EventStream> LoadStreamAsyncOrThrowNotFound(Guid streamId, string partitionKey, CancellationToken cancellationToken = default)
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

    public async Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey, CancellationToken cancellationToken = default)
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

    public async Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey, int fromVersion, CancellationToken cancellationToken = default)
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
            .Select(x => JsonSerializer.Deserialize<EventWrapper>(x, EventSerializerOptions.Options).GetEvent())
            .Where(x => !dateFrom.HasValue || x.Timestamp >= dateFrom)
            .OrderBy(x => x.Timestamp)
            .ToList();

        return events;
    }

    public async Task<bool> AppendToStreamAsync(
        EventUserInfo eventUserInfo,
        Guid streamId,
        int expectedVersion,
        IEnumerable<IEvent> events,
        CancellationToken cancellationToken = default
    )
    {
        if (events.GroupBy(x => x.PartitionKey).Count() != 1)
        {
            throw new ArgumentException("Partition keys for all events in the stream must be the same");
        }

        var lockObject = new object();
        lock (lockObject)
        {
            var partitionKey = events.First().PartitionKey;

            // Load stream and verify version hasn't been changed yet.
            var eventStream = LoadStreamAsync(streamId, partitionKey).GetAwaiter().GetResult();

            if (eventStream.Version != expectedVersion)
            {
                return false;
            }

            var wrappers = PrepareEvents(eventUserInfo, streamId, expectedVersion, events);
            var stream = _eventsContainer.ContainsKey((streamId, partitionKey))
                ? _eventsContainer[(streamId, partitionKey)]
                : new List<string>();

            foreach (var wrapper in wrappers)
            {
                stream.Add(JsonSerializer.Serialize(wrapper, EventSerializerOptions.Options));
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

        foreach (var e in events)
        {
            foreach (var h in _eventAddedEventHandlers)
            {
                await h(e);
            }
        }

        return true;
    }

    private async Task<List<EventWrapper>> LoadOrderedEventWrappers(Guid streamId, string partitionKey)
    {
        List<string> eventData = _eventsContainer.ContainsKey((streamId, partitionKey))
            ? _eventsContainer[(streamId, partitionKey)]
            : new List<string>();

        var eventWrappers = new List<EventWrapper>();

        foreach (var data in eventData)
        {
            var eventWrapper = JsonSerializer.Deserialize<EventWrapper>(data, EventSerializerOptions.Options);
            eventWrappers.Add(eventWrapper);
        }

        eventWrappers = eventWrappers.OrderBy(x => x.StreamInfo.Version).ToList();
        return eventWrappers;
    }

    private async Task<List<EventWrapper>> LoadOrderedEventWrappersFromVersion(Guid streamId, string partitionKey, int version)
    {
        List<string> eventData =
            _eventsContainer.ContainsKey((streamId, partitionKey))
                ? _eventsContainer[(streamId, partitionKey)]
                : new List<string>();
        var eventWrappers = new List<EventWrapper>();

        foreach (var data in eventData)
        {
            var eventWrapper = JsonSerializer.Deserialize<EventWrapper>(data, EventSerializerOptions.Options);
            if (eventWrapper.StreamInfo.Version >= version)
            {
                eventWrappers.Add(eventWrapper);
            }
        }

        eventWrappers = eventWrappers.OrderBy(x => x.StreamInfo.Version).ToList();
        return eventWrappers;
    }

    private static List<EventWrapper> PrepareEvents(
        EventUserInfo eventUserInfo, Guid streamId, int expectedVersion, IEnumerable<IEvent> events
    )
    {
        if (eventUserInfo.UserId == Guid.Empty)
            throw new Exception("UserInfo.Id must be set to a value.");

        var items = events.Select(
            e => new EventWrapper
            {
                // Id = $"{streamId}:{++expectedVersion}:{e.GetType().Name}",
                Id = Guid.NewGuid(), //:{e.GetType().Name}",
                StreamInfo = new StreamInfo { Id = streamId, Version = ++expectedVersion },
                EventType = e.GetType().AssemblyQualifiedName,
                EventData = JsonSerializer.SerializeToElement(e, e.GetType(), EventSerializerOptions.Options),
                UserInfo = JsonSerializer.SerializeToElement(eventUserInfo, eventUserInfo.GetType(), EventSerializerOptions.Options)
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