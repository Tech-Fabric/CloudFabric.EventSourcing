using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;


/// <summary>
/// This class represents index state for particular schema version.
/// When schema is changed and index needs to be rebuilt, we create a new IndexStateForSchemaVersion
/// for new schema properties hash. This index will be used to track projections rebuild progress.
/// </summary>
public record IndexStateForSchemaVersion
{
    public string IndexName { get; set; }
    
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Hash of all properties and their configuration so that we can easily identify particular schema version.
    /// </summary>
    public string SchemaHash { get; set; }

    public DateTime? RebuildCompletedAt { get; set; }

    public DateTime? RebuildStartedAt { get; set; }
    
    public long RebuildEventsProcessed { get; set; }

    public long TotalEventsToProcess { get; set; }
    
}

public class ProjectionIndexState
{
    public string ProjectionName { get; set; }

    /// <summary>
    /// This dictionary holds index status for every schema version.
    /// Each IndexStatus record has a SchemaHash property - a hash of all schema properties (their names, types and configurations);
    /// This allows having multiple indexes - one for each schema version.
    /// The basic scenario for schema update is - create a new additional index, start projections rebuild process, switch to new index once rebuild is completed.
    /// To make the switch flawless and not loose any events that may still be written to old index, we have to check this dictionary
    /// on every database write request. TODO: add redis cache for ProjectionIndexState
    /// </summary>
    public List<IndexStateForSchemaVersion> IndexesStatuses { get; set; }
}

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