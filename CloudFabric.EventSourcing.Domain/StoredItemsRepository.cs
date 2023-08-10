using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.EventSourcing.Domain;

public  class StoredItemsRepository : IStoredItemsRepository
{
    private readonly IEventStore _eventStore;

    public StoredItemsRepository(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task UpsertItem<TItem>(string id, string partitionKey, TItem item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        await _eventStore.UpsertItem(id, partitionKey, item, cancellationToken);
    }

    public async Task<TItem?> LoadItem<TItem>(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        return await _eventStore.LoadItem<TItem?>(id, partitionKey, cancellationToken);
    }

}
