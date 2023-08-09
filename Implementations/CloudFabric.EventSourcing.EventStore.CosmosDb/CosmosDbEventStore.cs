using System.Text.Json;
using CloudFabric.EventSourcing.EventStore.Persistence;
using Microsoft.Azure.Cosmos;

namespace CloudFabric.EventSourcing.EventStore.CosmosDb;

public class CosmosDbEventStore : IEventStore
{
    private readonly CosmosClient _client;
    private readonly string _eventsContainerId;
    private readonly string _itemsContainerId;
    private readonly string _databaseId;

    public CosmosDbEventStore(
        string connectionString,
        CosmosClientOptions cosmosClientOptions,
        string databaseId,
        string eventsContainerId,
        string itemsContainerId
    )
    {
        _client = new CosmosClient(connectionString, cosmosClientOptions);
        _databaseId = databaseId;
        _eventsContainerId = eventsContainerId;
        _itemsContainerId = itemsContainerId;
    }

    public CosmosDbEventStore(
        CosmosClient client,
        string databaseId,
        string eventsContainerId,
        string itemsContainerId
    )
    {
        _client = client;
        _databaseId = databaseId;
        _eventsContainerId = eventsContainerId;
        _itemsContainerId = itemsContainerId;
    }

    public Task Initialize(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task DeleteAll(CancellationToken cancellationToken = default)
    {
        var eventsContainer = _client.GetContainer(_databaseId, _eventsContainerId);

        try
        {
            await eventsContainer.DeleteContainerAsync(cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            if (ex.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                throw;
            }
        }

        var itemsContainer = _client.GetContainer(_databaseId, _itemsContainerId);

        try
        {
            await itemsContainer.DeleteContainerAsync(cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            if (ex.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                throw;
            }
        }
    }

    public async Task<bool> HardDeleteAsync(Guid streamId, string partitionKey, CancellationToken cancellationToken = default)
    {
        var container = _client.GetContainer(_databaseId, _eventsContainerId);

        var sqlQueryText = $"SELECT * FROM {_eventsContainerId} e" + " WHERE e.stream.id = @streamId";

        QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText)
            .WithParameter("@streamId", streamId);

        PartitionKey searchedPartitionKey = new PartitionKey(partitionKey);

        FeedIterator<EventWrapper> feedIterator = container.GetItemQueryIterator<EventWrapper>(
            queryDefinition,
            requestOptions: new QueryRequestOptions { PartitionKey = searchedPartitionKey }
        );

        List<TransactionalBatch> batches = new List<TransactionalBatch>
        {
            container.CreateTransactionalBatch(searchedPartitionKey)
        };

        var recordsIdCounter = new List<Guid>();

        // TO DO: Implement container.DeleteAllItemsByPartitionKeyStreamAsync(new PartitionKey("string")).
        // If we will use CloudFabric.EAV - partiton key in inherited from AgrefateBase classes
        // should be constructed like "Id"+"Entity" for this implementation
        while (feedIterator.HasMoreResults)
        {
            FeedResponse<EventWrapper> response = await feedIterator.ReadNextAsync();
            foreach (var eventWrapper in response)
            {
                batches.Last().DeleteItem(eventWrapper.Id.ToString());

                recordsIdCounter.Add(eventWrapper.Id!.Value);

                // Use max count of operations 100 due to the transactional batch limits.
                // For more info see https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/transactional-batch?tabs=dotnet.
                if (recordsIdCounter.Count == 100)
                {
                    batches.Add(container.CreateTransactionalBatch(searchedPartitionKey));

                    recordsIdCounter.Clear();
                }
            }
        }

        List<TransactionalBatchResponse> transactionalResults = new();

        await Parallel.ForEachAsync(batches, async (batch, cancellationToken) =>
            {
                transactionalResults.Add(await batch.ExecuteAsync(CancellationToken.None).ConfigureAwait(false));
            }
        ).ConfigureAwait(false);

        if (transactionalResults.Any(x => !x.IsSuccessStatusCode))
        {
            return false;
        }

        return true;
    }

    public async Task<EventStream> LoadStreamAsyncOrThrowNotFound(Guid streamId, string partitionKey, CancellationToken cancellationToken = default)
    {
        Container container = _client.GetContainer(_databaseId, _eventsContainerId);

        var sqlQueryText = $"SELECT * FROM {_eventsContainerId} e" + " WHERE e.stream.id = @streamId " + " ORDER BY e.stream.version";

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
            FeedResponse<EventWrapper> response = await feedIterator.ReadNextAsync(cancellationToken);
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

    public async Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey, CancellationToken cancellationToken = default)
    {
        Container container = _client.GetContainer(_databaseId, _eventsContainerId);

        var sqlQueryText = $"SELECT * FROM {_eventsContainerId} e" + " WHERE e.stream.id = @streamId " + " ORDER BY e.stream.version";

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
            FeedResponse<EventWrapper> response = await feedIterator.ReadNextAsync(cancellationToken);
            foreach (var eventWrapper in response)
            {
                version = eventWrapper.StreamInfo.Version;

                events.Add(eventWrapper.GetEvent());
            }
        }

        return new EventStream(streamId, version, events);
    }

    public async Task<EventStream> LoadStreamAsync(Guid streamId, string partitionKey, int fromVersion, CancellationToken cancellationToken = default)
    {
        Container container = _client.GetContainer(_databaseId, _eventsContainerId);

        var sqlQueryText = $"SELECT * FROM {_eventsContainerId} e" +
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
            FeedResponse<EventWrapper> response = await feedIterator.ReadNextAsync(cancellationToken);
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
        IEnumerable<IEvent> events,
        CancellationToken cancellationToken = default
    )
    {
        if (events.GroupBy(x => x.PartitionKey).Count() != 1)
        {
            throw new ArgumentException("Partition keys for all events in the stream must be the same");
        }

        Container container = _client.GetContainer(_databaseId, _eventsContainerId);

        PartitionKey cosmosPartitionKey = new PartitionKey(events.First().PartitionKey);

        dynamic[] parameters = new dynamic[]
        {
            streamId,
            expectedVersion,
            SerializeEvents(eventUserInfo, streamId, expectedVersion, events)
        };

        return await container.Scripts.ExecuteStoredProcedureAsync<bool>("spAppendToStream", cosmosPartitionKey, parameters, cancellationToken: cancellationToken);
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
                EventData = JsonSerializer.SerializeToElement(e, e.GetType(), EventStoreSerializerOptions.Options),
                UserInfo = JsonSerializer.SerializeToElement(eventUserInfo, eventUserInfo.GetType(), EventStoreSerializerOptions.Options)
            }
        );

        return JsonSerializer.Serialize(items, EventStoreSerializerOptions.Options);
    }

    #region Item Functionality

    public async Task UpsertItem<T>(string id, string partitionKey, T item, CancellationToken cancellationToken = default)
    {
        Container container = _client.GetContainer(_databaseId, _itemsContainerId);

        PartitionKey cosmosPartitionKey = new PartitionKey(partitionKey);

        var response = await container.UpsertItemAsync(
            new ItemWrapper
            {
                Id = id,
                PartitionKey = partitionKey,
                ItemData = JsonSerializer.Serialize(item, EventStoreSerializerOptions.Options)
            },
            cosmosPartitionKey,
            null,
            cancellationToken
        );

        if (response.StatusCode != System.Net.HttpStatusCode.OK && response.StatusCode != System.Net.HttpStatusCode.Created)
        {
            throw new Exception($"Cosmos Db returned status {response.StatusCode}.");
        }
    }

    public async Task<T?> LoadItem<T>(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        Container container = _client.GetContainer(_databaseId, _itemsContainerId);

        PartitionKey cosmosPartitionKey = new PartitionKey(partitionKey);

        var sqlQueryText = $"SELECT * FROM {_itemsContainerId} i" + " WHERE i.id = @id OFFSET 0 LIMIT 1";

        QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText)
            .WithParameter("@id", id);

        FeedIterator<ItemWrapper> feedIterator = container.GetItemQueryIterator<ItemWrapper>(
            queryDefinition,
            requestOptions: new QueryRequestOptions { PartitionKey = cosmosPartitionKey }
        );

        while (feedIterator.HasMoreResults)
        {
            FeedResponse<ItemWrapper> response = await feedIterator.ReadNextAsync(cancellationToken);

            if (response.Count == 0)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(response.First().ItemData, EventStoreSerializerOptions.Options);
        }

        return default;
    }

    #endregion
}
