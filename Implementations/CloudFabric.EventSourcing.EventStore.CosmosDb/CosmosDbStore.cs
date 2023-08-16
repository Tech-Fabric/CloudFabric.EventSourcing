using System.Text.Json;
using CloudFabric.EventSourcing.EventStore.Persistence;
using Microsoft.Azure.Cosmos;

namespace CloudFabric.EventSourcing.EventStore.CosmosDb;
public class CosmosDbStore : IStore
{
    private readonly CosmosClient _client;
    private readonly string _itemsContainerId;
    private readonly string _databaseId;


    public CosmosDbStore(
        string connectionString,
        CosmosClientOptions cosmosClientOptions,
        string databaseId,
        string itemsContainerId
    )
    {
        _client = new CosmosClient(connectionString, cosmosClientOptions);
        _databaseId = databaseId;
        _itemsContainerId = itemsContainerId;
    }

    public CosmosDbStore(
        CosmosClient client,
        string databaseId,
        string itemsContainerId
    )
    {
        _client = client;
        _databaseId = databaseId;
        _itemsContainerId = itemsContainerId;
    }

    public Task Initialize(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task DeleteAll(CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return;
        }

        var container = _client.GetContainer(_databaseId, _itemsContainerId);

        if (container != null)
        {
            try
            {
                await container.DeleteContainerAsync(cancellationToken: cancellationToken);
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw;
                }
            }
        }
    }

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
}
