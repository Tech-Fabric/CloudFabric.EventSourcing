namespace CloudFabric.EventSourcing.Domain;

/// <summary>
/// Repository that provides additional posisbility to store objects via IEventStore.
/// </summary>
public interface IStoredItemsRepository
{
    Task UpsertItem<T>(string id, string partitionKey, T item, CancellationToken cancellationToken = default);

    Task<T?> LoadItem<T>(string id, string partitionKey, CancellationToken cancellationToken = default);
}
