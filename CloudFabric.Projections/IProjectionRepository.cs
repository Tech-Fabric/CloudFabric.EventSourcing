using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;

public interface IProjectionRepository
{
    Task EnsureIndex(CancellationToken cancellationToken = default);
    Task<Dictionary<string, object?>?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default);
    Task<ProjectionQueryResult<Dictionary<string, object?>>> Query(ProjectionQuery projectionQuery, string? partitionKey = null, CancellationToken cancellationToken = default);
    Task Upsert(Dictionary<string, object?> document, string partitionKey, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task Delete(Guid id, string partitionKey, CancellationToken cancellationToken = default);
    Task DeleteAll(string? partitionKey = null, CancellationToken cancellationToken = default);
}

public interface IProjectionRepository<TDocument> : IProjectionRepository
    where TDocument : ProjectionDocument
{
    new Task<TDocument?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default);
    new Task<ProjectionQueryResult<TDocument>> Query(ProjectionQuery projectionQuery, string? partitionKey = null, CancellationToken cancellationToken = default);
    Task Upsert(TDocument document, string partitionKey, DateTime updatedAt, CancellationToken cancellationToken = default);
}