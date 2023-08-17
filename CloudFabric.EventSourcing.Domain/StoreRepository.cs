using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.EventSourcing.Domain;

public class StoreRepository : IStoreRepository
{
    private readonly IStore _store;

    public StoreRepository(IStore store) 
    {
        _store = store;
    }

    public async Task UpsertItem<TItem>(string id, string partitionKey, TItem item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        await _store.UpsertItem(id, partitionKey, item, cancellationToken);
    }

    public async Task<TItem?> LoadItem<TItem>(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException(nameof(id));
        }

        return await _store.LoadItem<TItem?>(id, partitionKey, cancellationToken);
    }
}
