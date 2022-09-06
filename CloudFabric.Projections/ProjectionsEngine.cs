using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public class ProjectionsEngine : IProjectionsEngine
{
    private readonly List<IProjectionBuilder<ProjectionDocument>> _projectionBuilders = new();
    private IEventsObserver? _observer; 
    private Dictionary<string, ProjectionRebuildState> _rebuildStates = new();

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
        _rebuildStates.Add(
            instanceName,
            new ProjectionRebuildState { Status = RebuildStatus.Running }
        );

        return _observer.LoadAndHandleEventsAsync(instanceName, dateFrom, OnRebuildCompleted, OnRebuildFailed);
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

    private void OnRebuildCompleted(string instanceName)
    {
        if (_rebuildStates.ContainsKey(instanceName))
        {
            _rebuildStates[instanceName].Status = RebuildStatus.Completed;
        }
    }

    private void OnRebuildFailed(string instanceName, string errorMessage)
    {
        if (_rebuildStates.ContainsKey(instanceName))
        {
            _rebuildStates[instanceName].Status = RebuildStatus.Failed;
            _rebuildStates[instanceName].ErrorMessage = errorMessage;
        }
    }
}
