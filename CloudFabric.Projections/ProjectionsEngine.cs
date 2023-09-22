using CloudFabric.EventSourcing.EventStore;
using CloudFabric.Projections.Queries;

namespace CloudFabric.Projections;

public class ProjectionsEngine : IProjectionsEngine
{
    private readonly List<IProjectionBuilder<ProjectionDocument>> _projectionBuilders = new();
    private readonly List<IProjectionBuilder> _dynamicProjectionBuilders = new();
    
    private EventsObserver? _observer;

    public ProjectionsEngine(
    ) {
    }

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

    public void SetEventsObserver(EventsObserver eventsObserver)
    {
        _observer = eventsObserver;
        _observer.SetEventHandler(HandleEvent);
    }

    public void AddProjectionBuilder(IProjectionBuilder<ProjectionDocument> projectionBuilder)
    {
        _projectionBuilders.Add(projectionBuilder);
    }
    
    public void AddProjectionBuilder(IProjectionBuilder projectionBuilder)
    {
        _dynamicProjectionBuilders.Add(projectionBuilder);
    }

    public async Task RebuildOneAsync(Guid documentId, string partitionKey)
    {
        if (_observer == null)
        {
            throw new InvalidOperationException("SetEventsObserver should be called before RebuildAsync");
        }
        
        await _observer.ReplayEventsForOneDocumentAsync(documentId, partitionKey);
    }

    private async Task HandleEvent(IEvent @event)
    {
        foreach (var projectionBuilder in
                 _projectionBuilders.Where(p => p.HandledEventTypes.Contains(@event.GetType())))
        {
            await projectionBuilder.ApplyEvent(@event); 
        }
        
        foreach (var projectionBuilder in
                 _dynamicProjectionBuilders.Where(p => p.HandledEventTypes.Contains(@event.GetType())))
        {
            await projectionBuilder.ApplyEvent(@event); 
        }
        
        #region Apply AggregateUpdatedEvent to projection builders

        var aggregateType = Type.GetType(@event.AggregateType);

        if (aggregateType == null)
        {
            throw new Exception($"Failed to get type of aggregate {@event.AggregateType}");
        }
        
        var aggregateUpdatedEventType = typeof(AggregateUpdatedEvent<>).MakeGenericType(aggregateType);

        var buildersWithAggregateUpdatedEvent = _projectionBuilders.Where(
            p => !p.HandledEventTypes.Contains(@event.GetType()) && p.HandledEventTypes.Contains(aggregateUpdatedEventType)
        ).ToList();

        var dynamicBuildersWithAggregateUpdatedEvent = _dynamicProjectionBuilders.Where(
            p => !p.HandledEventTypes.Contains(@event.GetType()) && p.HandledEventTypes.Contains(aggregateUpdatedEventType)
        ).ToList();

        if (buildersWithAggregateUpdatedEvent.Any() || dynamicBuildersWithAggregateUpdatedEvent.Any())
        {
            var aggregateUpdatedEvent = (IEvent)Activator.CreateInstance(aggregateUpdatedEventType)!;
            aggregateUpdatedEvent.AggregateId = @event.AggregateId;
            aggregateUpdatedEvent.PartitionKey = @event.PartitionKey;
            aggregateUpdatedEvent.AggregateType = @event.AggregateType;
            aggregateUpdatedEventType.GetProperty(nameof(AggregateUpdatedEvent<object>.UpdatedAt))!.SetValue(aggregateUpdatedEvent, @event.Timestamp);

            foreach (var projectionBuilder in buildersWithAggregateUpdatedEvent)
            {
                await projectionBuilder.ApplyEvent(aggregateUpdatedEvent);
            }

            foreach (var projectionBuilder in dynamicBuildersWithAggregateUpdatedEvent)
            {
                await projectionBuilder.ApplyEvent(aggregateUpdatedEvent);
            }
        }

        #endregion
    }

    public async Task ReplayEventsAsync(
        string instanceName, 
        string? partitionKey, 
        DateTime? dateFrom,
        int chunkSize = 250,
        Func<int, IEvent, Task>? chunkProcessedCallback = null,
        CancellationToken cancellationToken = default
    ) {
        if (_observer == null)
        {
            throw new InvalidOperationException("SetEventsObserver should be called before ReplayEventsAsync");
        }
        
        await _observer.ReplayEventsAsync(
            instanceName, 
            partitionKey, 
            dateFrom,
            chunkSize,
            chunkProcessedCallback,
            cancellationToken
        );
    }

    public async Task<EventStoreStatistics> GetEventStoreStatistics()
    {
        return await _observer.GetEventStoreStatistics();
    }
}