using CloudFabric.Projections.Exceptions;
using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;


public enum ProjectionOperationIndexSelector
{
    /// <summary>
    /// The most complete and up-to-date index, if exists, will be used, even if properties hash is different (meaning it's an old schema),
    /// new indices in process of rebuild will be ignored.
    /// If there is no complete index, a new index which is being rebuilt will be used.
    /// </summary>
    ReadOnly,
    /// <summary>
    /// The most complete and up-to-date index, if exists, will be used, even if properties hash is different (meaning it's an old schema),
    /// new indices in process of rebuild will be ignored.
    /// If there is no complete index, an exception will be thrown saying that index is not ready yet.
    /// </summary>
    Write,
    /// <summary>
    /// Only the most recent index with exact same schema properties hash will be used. This type is internal and should only be used
    /// when rebuilding projections.
    /// </summary>
    ProjectionRebuild
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
    public abstract Task<Dictionary<string, object?>?> Single(
        Guid id, 
        string partitionKey, 
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    );
    public abstract Task<ProjectionQueryResult<Dictionary<string, object?>>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    );
    public abstract Task Upsert(
        Dictionary<string, object?> document, 
        string partitionKey,
        DateTime updatedAt,
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    );
    public abstract Task Delete(
        Guid id, 
        string partitionKey, 
        CancellationToken cancellationToken = default, 
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    );
    public abstract Task DeleteAll(
        string? partitionKey = null, 
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    );

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
    protected async Task<string?> GetIndexNameForSchema(ProjectionOperationIndexSelector indexSelector, CancellationToken cancellationToken = default)
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
                await CreateIndex(projectionVersionIndexName, ProjectionDocumentSchema);
                await SaveProjectionIndexState(projectionIndexState);
            }

            if (indexSelector == ProjectionOperationIndexSelector.ProjectionRebuild)
            {
                return projectionVersionIndexName;
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
            if (indexSelector == ProjectionOperationIndexSelector.ReadOnly)
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

    public abstract Task<(ProjectionIndexState?, string?)> AcquireAndLockProjectionThatRequiresRebuild();
    public abstract Task UpdateProjectionRebuildStats(ProjectionIndexState indexToRebuild);
}