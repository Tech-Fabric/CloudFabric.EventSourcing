namespace CloudFabric.EventSourcing.EventStore;

public interface IStore
{
    Task Initialize(CancellationToken cancellationToken = default);

    Task DeleteAll(CancellationToken cancellationToken = default);

    Task UpsertItem<T>(string id, string partitionKey, T item, CancellationToken cancellationToken = default);

    Task<T?> LoadItem<T>(string id, string partitionKey, CancellationToken cancellationToken = default);
}
