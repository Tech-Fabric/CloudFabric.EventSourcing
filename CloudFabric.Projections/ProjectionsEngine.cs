using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public class ProjectionsEngine : IProjectionsEngine
{
    private readonly List<IProjectionBuilder<ProjectionDocument>> _projectionBuilders = new();
    private IEventsObserver? _observer;

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

    private async Task<bool> HandleEvent(IEvent @event)
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

        return true;
    }

    public void AddProjectionBuilder(IProjectionBuilder<ProjectionDocument> projectionBuilder)
    {
        _projectionBuilders.Add(projectionBuilder);
    }
}
