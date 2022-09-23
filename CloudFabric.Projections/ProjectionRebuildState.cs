
using CloudFabric.Projections.Attributes;

namespace CloudFabric.Projections;

[ProjectionDocument]
public class ProjectionRebuildState : ProjectionDocument
{
    [ProjectionDocumentProperty(IsSearchable = true)]
    public string InstanceName { get; set;}
    
    [ProjectionDocumentProperty(IsFilterable = true)]
    public RebuildStatus Status { get; set; } = RebuildStatus.NotRun;

    [ProjectionDocumentProperty]
    public string ErrorMessage { get; set; }
}

public enum RebuildStatus
{
    NotRun = 1,
    Running = 2,
    Completed = 3,
    Failed = 4
}