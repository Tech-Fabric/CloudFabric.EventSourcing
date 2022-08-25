using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;

public interface IProjectionRepository
{
    Task<Dictionary<string, object?>?> Single(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Dictionary<string, object?>>> Query(ProjectionQuery projectionQuery, CancellationToken cancellationToken = default);
    Task Upsert(Dictionary<string, object?> document, CancellationToken cancellationToken = default);
    Task Delete(string id, CancellationToken cancellationToken = default);
    Task DeleteAll(CancellationToken cancellationToken = default);
}

public interface IProjectionRepository<TDocument> : IProjectionRepository
    where TDocument : ProjectionDocument
{
    new Task<TDocument?> Single(string id, CancellationToken cancellationToken = default);
    new Task<IReadOnlyCollection<TDocument>> Query(ProjectionQuery projectionQuery, CancellationToken cancellationToken = default);
    Task Upsert(TDocument document, CancellationToken cancellationToken = default);
}