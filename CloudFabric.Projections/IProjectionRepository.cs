using CloudFabric.Projections.Attributes;
using CloudFabric.Projections.Exceptions;
using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;


/// <summary>
/// This class represents index state for particular schema version.
/// When schema is changed and index needs to be rebuilt, we create a new IndexStateForSchemaVersion
/// for new schema properties hash. This index will be used to track projections rebuild progress.
/// </summary>
public record IndexStateForSchemaVersion
{
    [ProjectionDocumentProperty(IsFilterable = true)]
    public string IndexName { get; set; }
    
    [ProjectionDocumentProperty(IsFilterable = true)]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Hash of all properties and their configuration so that we can easily identify particular schema version.
    /// </summary>
    [ProjectionDocumentProperty(IsFilterable = true)]
    public string SchemaHash { get; set; }

    [ProjectionDocumentProperty(IsFilterable = true)]
    public DateTime? RebuildCompletedAt { get; set; }

    [ProjectionDocumentProperty(IsFilterable = true)]
    public DateTime? RebuildStartedAt { get; set; }
    
    [ProjectionDocumentProperty(IsFilterable = true)]
    public DateTime? RebuildHealthCheckAt { get; set; }
    
    [ProjectionDocumentProperty(IsFilterable = true)]
    public long RebuildEventsProcessed { get; set; }

    [ProjectionDocumentProperty(IsFilterable = true)]
    public long TotalEventsToProcess { get; set; }

    [ProjectionDocumentProperty(IsFilterable = true)]
    public DateTime? LastProcessedEventTimestamp { get; set; }
}

[ProjectionDocument]
public class ProjectionIndexState : ProjectionDocument
{
    public string ProjectionName { get; set; }

    /// <summary>
    /// @see IPostgresqlEventStoreConnectionInformationProvider implementation documentation.
    ///
    /// There can be multiple event store databases, on different hosts - *EventStoreConnectionInformationProvider may provide connection strings
    /// based on FROM ip address, or tenant name, or anything else.
    ///
    /// When we need to re-construct the event store to rebuild it's projections, we will use this ConnectionId to obtain previous connection information from
    /// EventStoreConnectionInformationProvider. 
    /// </summary>
    [ProjectionDocumentProperty(IsFilterable = true)]
    public string ConnectionId { get; set; }

    /// <summary>
    /// This dictionary holds index status for every schema version.
    /// Each IndexStatus record has a SchemaHash property - a hash of all schema properties (their names, types and configurations);
    /// This allows having multiple indexes - one for each schema version.
    /// The basic scenario for schema update is - create a new additional index, start projections rebuild process, switch to new index once rebuild is completed.
    /// To make the switch flawless and not loose any events that may still be written to old index, we have to check this dictionary
    /// on every database write request. TODO: add redis cache for ProjectionIndexState
    /// </summary>
    [ProjectionDocumentProperty(IsNestedArray = true)]
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

public abstract class ProjectionRepository : IProjectionRepository
{
    protected readonly ProjectionDocumentSchema ProjectionDocumentSchema;

    public ProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        ProjectionDocumentSchema = projectionDocumentSchema;
    }

    public abstract Task EnsureIndex(CancellationToken cancellationToken = default);
    protected abstract Task CreateIndex(string indexName, ProjectionDocumentSchema projectionDocumentSchema);
    public abstract Task<Dictionary<string, object?>?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default);
    public abstract Task<ProjectionQueryResult<Dictionary<string, object?>>> Query(ProjectionQuery projectionQuery, string? partitionKey = null, CancellationToken cancellationToken = default);
    public abstract Task Upsert(Dictionary<string, object?> document, string partitionKey, DateTime updatedAt, CancellationToken cancellationToken = default);
    public abstract Task Delete(Guid id, string partitionKey, CancellationToken cancellationToken = default);
    public abstract Task DeleteAll(string? partitionKey = null, CancellationToken cancellationToken = default);

    protected abstract Task<ProjectionIndexState?> GetProjectionIndexState(CancellationToken cancellationToken = default);
    protected abstract Task SaveProjectionIndexState(ProjectionIndexState state);

    /// <summary>
    /// Internal method for getting index name to work with.
    /// When projection schema changes, there can be two indexes - one for old version of schema which should still receive updates and queries
    /// and a new one which should be populated in the background.
    /// This method checks for available indexes and selects the active one. Once a new index is completed projections rebuild process, this method
    /// will immediately return a new one, so all updates will go to the new index. 
    /// </summary>
    /// <returns></returns>
    protected async Task<string?> GetIndexNameForSchema(bool readOnly, CancellationToken cancellationToken = default)
    {
        var projectionIndexState = await GetProjectionIndexState(cancellationToken);
        
        var projectionVersionPropertiesHash = ProjectionDocumentSchemaFactory.GetPropertiesUniqueHash(ProjectionDocumentSchema.Properties);
        var projectionVersionIndexName = $"{ProjectionDocumentSchema.SchemaName}_{projectionVersionPropertiesHash}"
            .ToLower(); // Elastic throws error saying that index must be lowercase

        if (projectionIndexState != null)
        {
            // First of all - check if index statuses contains an index for this particular schema version
            var indexStatusForThisSchemaVersion = projectionIndexState.IndexesStatuses
                .FirstOrDefault(indexStatus => indexStatus.SchemaHash == projectionVersionPropertiesHash);
            
            if (indexStatusForThisSchemaVersion == null)
            {
                // If it does not, we need to create it so that it will be picked up by projections rebuild processor
                projectionIndexState.IndexesStatuses.Add(new IndexStateForSchemaVersion()
                {
                    CreatedAt = DateTime.UtcNow,
                    SchemaHash = projectionVersionPropertiesHash,
                    IndexName = projectionVersionIndexName,
                    RebuildEventsProcessed = 0,
                    RebuildStartedAt = null,
                    RebuildCompletedAt = null,
                    RebuildHealthCheckAt = DateTime.UtcNow
                });
                await SaveProjectionIndexState(projectionIndexState);
            }

            // At least some projection state exists - find the most recent index with completed projections rebuild
            var lastIndexWithRebuiltProjections = projectionIndexState.IndexesStatuses
                .Where(i => i.RebuildCompletedAt != null).MaxBy(i => i.RebuildCompletedAt);

            if (lastIndexWithRebuiltProjections != null)
            {
                return lastIndexWithRebuiltProjections.IndexName;
            }
            
            // At least some projection state exists but there is no index which was completely rebuilt. 
            // In such situation we could only allow reading from this index, because writing to it may break projections
            // events order consistency - if projections rebuild is still in progress we will write an event which happened now before it's preceding
            // events not yet processed by projections rebuild process.
            if (readOnly)
            {
                // if there are multiple indexes, we want one that has already started rebuild process.
                var lastIndexWithRebuildStarted = projectionIndexState.IndexesStatuses
                    .Where(i => i.RebuildStartedAt != null).MaxBy(i => i.RebuildStartedAt);

                if (lastIndexWithRebuildStarted != null)
                {
                    return lastIndexWithRebuildStarted.IndexName;
                }
                
                // If there are multiple indexes but none of them started rebuilding, just return the most recently created one.
                var lastIndex = projectionIndexState.IndexesStatuses
                    .MaxBy(i => i.CreatedAt);

                if (lastIndex != null)
                {
                    return lastIndex.IndexName;
                }
            }

            throw new IndexNotReadyException(projectionIndexState);
        }
        else
        {
            // no index state exists, meaning there is no index at all. 
            // Create an empty index state, index background processor is designed to look for records which 
            // were created but not populated, it will start the process of projections rebuild once it finds this new record.
            
            var newProjectionIndexState = new ProjectionIndexState()
            {
                ProjectionName = ProjectionDocumentSchema.SchemaName,
                IndexesStatuses = new List<IndexStateForSchemaVersion>() {
                    new IndexStateForSchemaVersion()
                    {
                        CreatedAt = DateTime.UtcNow,
                        SchemaHash = projectionVersionPropertiesHash,
                        IndexName = projectionVersionIndexName,
                        RebuildEventsProcessed = 0,
                        RebuildStartedAt = null,
                        RebuildCompletedAt = null,
                        RebuildHealthCheckAt = DateTime.UtcNow
                    }
                }
            };

            await CreateIndex(projectionVersionIndexName, ProjectionDocumentSchema);
            await SaveProjectionIndexState(newProjectionIndexState);

            return projectionVersionIndexName;
        }
    }

    public abstract Task<ProjectionIndexState?> AcquireAndLockProjectionThatRequiresRebuild();
    public abstract Task UpdateProjectionRebuildStats(ProjectionIndexState indexToRebuild);
}