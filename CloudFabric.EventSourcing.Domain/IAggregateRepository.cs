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

    Task<bool> HardDeleteAsync(Guid id, string partitionKey, CancellationToken cancellationToken = default);
}
