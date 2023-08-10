using CloudFabric.EventSourcing.EventStore.Persistence;

namespace CloudFabric.EventSourcing.Domain;

/// <summary>
/// Repository for working with domain entities
/// </summary>
/// <typeparam name="T">Domain entity derived from AggregateBase</typeparam>
public interface IAggregateRepository<T> where T : AggregateBase
{
    Task<T?> LoadAsync(Guid id, string partitionKey, CancellationToken cancellationToken = default);

    Task<T> LoadAsyncOrThrowNotFound(Guid id, string partitionKey, CancellationToken cancellationToken = default);

    Task<bool> SaveAsync(EventUserInfo eventUserInfo, T aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provides additional posisbility to store objects within AggregateRepository. 
    /// </summary>
    /// <typeparam name="TItem">Generic type parameter</typeparam>
    Task UpsertItem<TItem>(string id, string partitionKey, TItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load saved object.
    /// </summary>
    /// <typeparam name="TItem">Generic type parameter</typeparam>
    Task<TItem?> LoadItem<TItem>(string id, string partitionKey, CancellationToken cancellationToken = default);

    Task<bool> HardDeleteAsync(Guid id, string partitionKey, CancellationToken cancellationToken = default);
}
