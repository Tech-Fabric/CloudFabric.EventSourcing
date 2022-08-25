namespace CloudFabric.Projections;

// public abstract class ProjectionRepository: IProjectionRepository
// {
//     public abstract Task<object?> Single(string id, CancellationToken cancellationToken = default);
//     public abstract Task Upsert(object document, CancellationToken cancellationToken = default);
//     public abstract Task Delete(string id, CancellationToken cancellationToken = default);
//     public abstract Task DeleteAll(CancellationToken cancellationToken = default);
//     public abstract Task<IReadOnlyCollection<object>> Query(ProjectionQuery projectionQuery, CancellationToken cancellationToken = default);
// }
//
// public abstract class ProjectionRepository<TDocument> : IProjectionRepository<TDocument>
//     where TDocument : ProjectionDocument
// {
//     public abstract Task<TDocument?> Single(string id, CancellationToken cancellationToken = default);
//     public abstract Task Upsert(TDocument document, CancellationToken cancellationToken = default);
//     public abstract Task Delete(string id, CancellationToken cancellationToken = default);
//     public abstract Task DeleteAll(CancellationToken cancellationToken = default);
//     public abstract Task<IReadOnlyCollection<TDocument>> Query(ProjectionQuery projectionQuery, CancellationToken cancellationToken = default);
// }