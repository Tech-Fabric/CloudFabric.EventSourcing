using System.Text.Json;

namespace CloudFabric.EventSourcing.EventStore.InMemory;

public class InMemoryMetadataRepository : IMetadataRepository
{
    private readonly Dictionary<(string Id, string PartitionKey), string> _itemsContainer;

    public InMemoryMetadataRepository(Dictionary<(string Id, string PartitionKey), string> itemsContainer)
    {
        _itemsContainer = itemsContainer;
    }

    public Task Initialize(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAll(CancellationToken cancellationToken = default)
    {
        _itemsContainer.Clear();
        return Task.CompletedTask;
    }

    public async Task UpsertItem<T>(string id, string partitionKey, T item, CancellationToken cancellationToken = default)
    {
        var serializedItem = JsonSerializer.Serialize(item, EventStoreSerializerOptions.Options);

        var itemNotExists = _itemsContainer.TryAdd((id, partitionKey), serializedItem);

        if (!itemNotExists)
        {
            _itemsContainer[(id, partitionKey)] = serializedItem;
        }
    }

    public async Task<T?> LoadItem<T>(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        if (_itemsContainer.TryGetValue((id, partitionKey), out string? value))
        {
            return value != null
                ? JsonSerializer.Deserialize<T>(value, EventStoreSerializerOptions.Options)
                : default;
        }

        return default;
    }
}