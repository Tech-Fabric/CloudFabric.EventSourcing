namespace CloudFabric.EventSourcing.Domain;

/// <summary>
/// Repository that provides possibility of storing typed objects,
/// due to requirements to save non 'evented' aggregates items outside of EventStore.
/// </summary>
public interface IStoreRepository
{
    Task UpsertItem<T>(string id, string partitionKey, T item, CancellationToken cancellationToken = default);

    Task<T?> LoadItem<T>(string id, string partitionKey, CancellationToken cancellationToken = default);
}
