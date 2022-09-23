using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;

public interface IProjectionRepository
{
    Task<Dictionary<string, object?>?> Single(string id, string partitionKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Dictionary<string, object?>>> Query(ProjectionQuery projectionQuery, string? partitionKey = null, CancellationToken cancellationToken = default);
    Task Upsert(Dictionary<string, object?> document, string partitionKey, CancellationToken cancellationToken = default);
    Task Delete(string id, string partitionKey, CancellationToken cancellationToken = default);
    Task DeleteAll(string? partitionKey = null, CancellationToken cancellationToken = default);
}

public interface IProjectionRepository<TDocument> : IProjectionRepository
    where TDocument : ProjectionDocument
{
    new Task<TDocument?> Single(string id, string partitionKey, CancellationToken cancellationToken = default);
    new Task<IReadOnlyCollection<TDocument>> Query(ProjectionQuery projectionQuery, string? partitionKey = null, CancellationToken cancellationToken = default);
    Task Upsert(TDocument document, string partitionKey, CancellationToken cancellationToken = default);
}