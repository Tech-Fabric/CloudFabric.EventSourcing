
namespace CloudFabric.Projections;

public class ProjectionRebuildState
{
    public RebuildStatus Status { get; set; } = RebuildStatus.NotRun;

    public string ErrorMessage { get; set; }
}

public enum RebuildStatus
{
    NotRun,
    Running,
    Completed,
    Failed
}