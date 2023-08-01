using System.Text.Json;
using CloudFabric.Projections.Attributes;

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

    [ProjectionDocumentProperty]
    public string? Schema { get; set; }

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
    [ProjectionDocumentProperty(IsFilterable = true)]
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