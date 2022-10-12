using System.Text.Json;
using CloudFabric.EventSourcing.EventStore.Persistence;
using Microsoft.Azure.Cosmos;

namespace CloudFabric.EventSourcing.EventStore.CosmosDb;

public class CosmosDbEventStore : IEventStore
{
    private readonly CosmosClient _client;
    private readonly string _containerId;
    private readonly string _databaseId;

    public CosmosDbEventStore(
        string connectionString,
        CosmosClientOptions cosmosClientOptions,
        string databaseId,
        string containerId
    )
    {
        _client = new CosmosClient(connectionString, cosmosClientOptions);
        _databaseId = databaseId;
        _containerId = containerId;
    }

    public CosmosDbEventStore(
        CosmosClient client,
        string databaseId,
        string containerId
    )
    {
        _client = client;
        _databaseId = databaseId;
        _containerId = containerId;
    }

    public Task Initialize()
    {
        return Task.CompletedTask;
    }

    public async Task DeleteAll()
    {
        var container = _client.GetContainer(_databaseId, _containerId);
        await container.DeleteContainerAsync();
    }

    public async Task<EventStream> LoadStreamAsyncOrThrowNotFound(Guid streamId, string partitionKey)
    {
        Container container = _client.GetContainer(_databaseId, _containerId);

        var sqlQueryText = $"SELECT * FROM {_containerId} e" + " WHERE e.stream.id = @streamId " + " ORDER BY e.stream.version";

        QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText)
            .WithParameter("@streamId", streamId);

        int version = 0;
        var events = new List<IEvent>();

        FeedIterator<EventWrapper> feedIterator = container.GetItemQueryIterator<EventWrapper>(
            queryDefinition,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) }
        );
        while (feedIterator.HasMoreResults)
        {
            FeedResponse<EventWrapper> response = await feedIterator.ReadNextAsync();
            foreach (var eventWrapper in response)
            {
                version = eventWrapper.StreamInfo.Version;

                events.Add(eventWrapper.GetEvent());
            }
        }

        if (events.Count == 0)
        {
            throw new NotFoundException();
        }

        return new EventStream(streamId, version, events);
    }

    public async Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey)
    {
        Container container = _client.GetContainer(_databaseId, _containerId);

        var sqlQueryText = $"SELECT * FROM {_containerId} e" + " WHERE e.stream.id = @streamId " + " ORDER BY e.stream.version";

        QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText)
            .WithParameter("@streamId", streamId);

        int version = 0;
        var events = new List<IEvent>();

        FeedIterator<EventWrapper> feedIterator = container.GetItemQueryIterator<EventWrapper>(
            queryDefinition,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) }
        );
        while (feedIterator.HasMoreResults)
        {
            FeedResponse<EventWrapper> response = await feedIterator.ReadNextAsync();
            foreach (var eventWrapper in response)
            {
                version = eventWrapper.StreamInfo.Version;

                events.Add(eventWrapper.GetEvent());
            }
        }

        return new EventStream(streamId, version, events);
    }

    public async Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey, int fromVersion)
    {
        Container container = _client.GetContainer(_databaseId, _containerId);

        var sqlQueryText = $"SELECT * FROM {_containerId} e" +
                           " WHERE e.stream.id = @streamId AND e.stream.version >= @fromVersion" +
                           " ORDER BY e.stream.version";

        QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText)
            .WithParameter("@streamId", streamId)
            .WithParameter("@fromVersion", fromVersion);

        int version = 0;
        var events = new List<IEvent>();

        FeedIterator<EventWrapper> feedIterator = container.GetItemQueryIterator<EventWrapper>(
            queryDefinition,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) }
        );
        while (feedIterator.HasMoreResults)
        {
            FeedResponse<EventWrapper> response = await feedIterator.ReadNextAsync();
            foreach (var eventWrapper in response)
            {
                version = eventWrapper.StreamInfo.Version;
                events.Add(eventWrapper.GetEvent());
            }
        }

        return new EventStream(streamId, version, events);
    }

    public async Task<bool> AppendToStreamAsync(
        EventUserInfo eventUserInfo,
        Guid streamId,
        int expectedVersion,
        IEnumerable<IEvent> events
    )
    {
        if (events.GroupBy(x => x.PartitionKey).Count() != 1)
        {
            throw new ArgumentException("Partition keys for all events in the stream must be the same");
        }

        Container container = _client.GetContainer(_databaseId, _containerId);

        PartitionKey cosmosPartitionKey = new PartitionKey(events.First().PartitionKey);

        dynamic[] parameters = new dynamic[]
        {
            streamId,
            expectedVersion,
            SerializeEvents(eventUserInfo, streamId, expectedVersion, events)
        };

        return await container.Scripts.ExecuteStoredProcedureAsync<bool>("spAppendToStream", cosmosPartitionKey, parameters);
    }

    private static string SerializeEvents(
        EventUserInfo eventUserInfo,
        Guid streamId,
        int expectedVersion,
        IEnumerable<IEvent> events
    )
    {
        if (eventUserInfo.UserId == Guid.Empty)
            throw new Exception("UserInfo.Id must be set to a value.");

        var items = events.Select(
            e => new EventWrapper
            {
                Id = Guid.NewGuid(),
                StreamInfo = new StreamInfo
                {
                    Id = streamId,
                    Version = ++expectedVersion
                },
                EventType = e.GetType().AssemblyQualifiedName,
                EventData = JsonSerializer.SerializeToElement(e, e.GetType(), EventSerializerOptions.Options),
                UserInfo = JsonSerializer.SerializeToElement(eventUserInfo, eventUserInfo.GetType(), EventSerializerOptions.Options)
            }
        );

        return JsonSerializer.Serialize(items, EventSerializerOptions.Options);
    }
}
