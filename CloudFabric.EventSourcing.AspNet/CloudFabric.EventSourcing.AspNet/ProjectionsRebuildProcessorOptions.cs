namespace CloudFabric.EventSourcing.AspNet;

public class ProjectionsRebuildProcessorOptions
{
    public int MaxParallelTasks { get; set; } = 4;
}
