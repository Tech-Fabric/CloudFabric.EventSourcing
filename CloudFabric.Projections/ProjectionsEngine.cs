using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public enum RebuildStatus
{
    NotRun,
    Running,
    Completed,
    Failed
}

public class RebuildState
{
    public string InstanceName { get; set; }

    public RebuildStatus Status { get; set; } = RebuildStatus.NotRun;
}

public class ProjectionsEngine : IProjectionsEngine
{
    private readonly List<IProjectionBuilder<ProjectionDocument>> _projectionBuilders = new();
    private IEventsObserver? _observer; 
    private List<RebuildState> _rebuildStates = new();

    public Task StartAsync(string instanceName)
    {
        if (_observer == null)
        {
            throw new InvalidOperationException("SetEventsObserver should be called before StartAsync");
        }

        return _observer.StartAsync(instanceName);
    }

    public Task StopAsync()
    {
        if (_observer == null)
        {
            throw new InvalidOperationException("SetEventsObserver should be called before StopAsync");
        }

        return _observer.StopAsync();
    }

    public void SetEventsObserver(IEventsObserver eventsObserver)
    {
        _observer = eventsObserver;
        _observer.SetEventHandler(HandleEvent);
    }

    public void AddProjectionBuilder(IProjectionBuilder<ProjectionDocument> projectionBuilder)
    {
        _projectionBuilders.Add(projectionBuilder);
    }

    public Task RebuildAsync(string instanceName, DateTime? dateFrom = null)
    {
        return _observer.LoadAndHandleEventsAsync(instanceName, dateFrom);
    }

    public async Task RebuildOneAsync(string documentId)
    {
        await _observer.LoadAndHandleEventsForDocumentAsync(documentId);
    }

    private async Task HandleEvent(IEvent @event)
    {
        foreach (var projectionBuilder in
                 _projectionBuilders.Where(p => p.HandledEventTypes.Contains(@event.GetType())))
        {
            try
            {
                await projectionBuilder.ApplyEvent(@event);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
